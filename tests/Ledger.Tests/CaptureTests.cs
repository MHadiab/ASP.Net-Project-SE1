using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Ledger.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Tests;

public class CaptureTests : IClassFixture<PostgresTestFixture>
{
    private readonly PostgresTestFixture fixture;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public CaptureTests(PostgresTestFixture fixture) => this.fixture = fixture;

    private async Task<(Guid Hold, Guid Source, Guid Destination)> SeedAsync(
        bool blocked = false, bool expired = false, bool external = false)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = Guid.NewGuid();
        var destination = external
            ? await db.LedgerAccounts.Where(x => x.SystemCode == SystemAccountCode.CLEARING)
                .Select(x => (Guid?)x.Id).SingleOrDefaultAsync() ?? Guid.NewGuid()
            : Guid.NewGuid();
        db.LedgerAccounts.Add(new LedgerAccount
        {
            Id = source, AccountType = AccountType.CUSTOMER, PostingAllowed = true,
            Balance = new AccountBalance { AccountId = source, BookBalance = 100, HeldAmount = 100 }
        });
        if (!await db.LedgerAccounts.AnyAsync(x => x.Id == destination))
            db.LedgerAccounts.Add(new LedgerAccount
            {
                Id = destination, AccountType = external ? AccountType.SYSTEM : AccountType.CUSTOMER,
                SystemCode = external ? SystemAccountCode.CLEARING : null, PostingAllowed = !blocked,
                Balance = new AccountBalance { AccountId = destination }
            });
        var hold = new Hold
        {
            AccountId = source, Amount = 80, ReferenceId = Guid.NewGuid(),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expired ? -10 : 10), CorrelationId = "capture-test"
        };
        db.Holds.Add(hold);
        db.Holds.Add(new Hold { AccountId = source, Amount = 20, ReferenceId = Guid.NewGuid(),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10), CorrelationId = "capture-test" });
        await db.SaveChangesAsync();
        return (hold.Id, source, destination);
    }

    private async Task<HttpResponseMessage> SendAsync(Guid hold, string key, CaptureHoldRequest body)
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/holds/{hold}/capture");
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", "capture-http-test");
        request.Content = JsonContent.Create(body, options: Json);
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Http_capture_posts_once_and_replays_the_original_response(bool external)
    {
        var seed = await SeedAsync(external: external);
        var body = new CaptureHoldRequest(external ? "EXTERNAL" : "INTERNAL",
            external ? null : seed.Destination, "capture test");
        var key = Guid.NewGuid().ToString();
        using var first = await SendAsync(seed.Hold, key, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("application/json", first.Content.Headers.ContentType!.MediaType);
        Assert.Equal("capture-http-test", first.Headers.GetValues("X-Correlation-ID").Single());
        var response = (await first.Content.ReadFromJsonAsync<CaptureHoldResponse>(Json))!;
        Assert.Equal("CAPTURED", response.Hold.Status);
        Assert.Equal(response.Entry.EntryId, response.Hold.CaptureEntryId);
        Assert.Equal(external ? "TRANSFER_EXTERNAL" : "TRANSFER_INTERNAL", response.Entry.EntryType);
        Assert.Equal(2, response.Entry.Lines.Count);
        Assert.Equal(80L, response.Entry.Total.Amount);
        using var replay = await SendAsync(seed.Hold, key, body);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var balance = await db.AccountBalances.SingleAsync(x => x.AccountId == seed.Source);
        Assert.Equal(20L, balance.BookBalance);
        Assert.Equal(20L, balance.HeldAmount);
        Assert.Equal(1L, balance.Version);
        Assert.Equal(1, await db.JournalEntries.CountAsync(x => x.HoldId == seed.Hold));
        Assert.True(await db.Holds.AnyAsync(x => x.AccountId == seed.Source && x.Amount == 20
            && x.Status == HoldStatus.Active));

        using var conflict = await SendAsync(seed.Hold, key, body with { Description = "changed" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(true, false, 422, "ACCOUNT_BLOCKED")]
    [InlineData(false, true, 409, "HOLD_EXPIRED")]
    public async Task Http_failure_keeps_the_hold_and_all_balances_unchanged(
        bool blocked, bool expired, int status, string code)
    {
        var seed = await SeedAsync(blocked, expired);
        using var response = await SendAsync(seed.Hold, Guid.NewGuid().ToString(),
            new CaptureHoldRequest("INTERNAL", seed.Destination, null));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(HoldStatus.Active, (await db.Holds.FindAsync(seed.Hold))!.Status);
        var balance = (await db.AccountBalances.FindAsync(seed.Source))!;
        Assert.Equal(100L, balance.BookBalance);
        Assert.Equal(100L, balance.HeldAmount);
        Assert.False(await db.JournalEntries.AnyAsync(x => x.HoldId == seed.Hold));
    }

    [Fact]
    public async Task Concurrent_requests_for_one_hold_create_only_one_journal()
    {
        var seed = await SeedAsync();
        var body = new CaptureHoldRequest("INTERNAL", seed.Destination, null);
        var responses = await Task.WhenAll(SendAsync(seed.Hold, Guid.NewGuid().ToString(), body),
            SendAsync(seed.Hold, Guid.NewGuid().ToString(), body));
        try
        {
            Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.OK));
            Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Conflict));
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.JournalEntries.CountAsync(x => x.HoldId == seed.Hold));
            Assert.Equal(20L, (await db.AccountBalances.FindAsync(seed.Source))!.HeldAmount);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Concurrent_retries_with_the_same_key_replay_one_capture()
    {
        var seed = await SeedAsync();
        var body = new CaptureHoldRequest("INTERNAL", seed.Destination, null);
        var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(SendAsync(seed.Hold, key, body), SendAsync(seed.Hold, key, body));
        try
        {
            Assert.All(responses, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
            Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
            using var scope = fixture.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.JournalEntries.CountAsync(x => x.HoldId == seed.Hold));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData("missing-key", 400, "ERROR_VALIDATION")]
    [InlineData("missing-destination", 400, "ERROR_VALIDATION")]
    [InlineData("unknown-destination", 404, "NOT_FOUND")]
    [InlineData("unknown-hold", 404, "NOT_FOUND")]
    public async Task Invalid_http_requests_do_not_consume_the_reservation(string scenario, int status, string code)
    {
        var seed = await SeedAsync();
        using var response = await SendAsync(scenario == "unknown-hold" ? Guid.NewGuid() : seed.Hold,
            scenario == "missing-key" ? "" : Guid.NewGuid().ToString(),
            new CaptureHoldRequest("INTERNAL", scenario == "missing-destination" ? null
                : scenario == "unknown-destination" ? Guid.NewGuid() : seed.Destination, null));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(100L, (await db.AccountBalances.FindAsync(seed.Source))!.HeldAmount);
        Assert.Equal(HoldStatus.Active, (await db.Holds.FindAsync(seed.Hold))!.Status);
        Assert.False(await db.JournalEntries.AnyAsync(x => x.HoldId == seed.Hold));
    }

    [Fact]
    public async Task Posting_rejection_restores_the_reservation_and_preserves_the_original_error()
    {
        var seed = await SeedAsync();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AccountBalances.FindAsync(seed.Source))!.BookBalance = 70;
            await db.SaveChangesAsync();
        }
        using var response = await SendAsync(seed.Hold, Guid.NewGuid().ToString(),
            new CaptureHoldRequest("INTERNAL", seed.Destination, null));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INSUFFICIENT_FUNDS", problem.GetProperty("code").GetString());
        using var verify = fixture.Factory.Services.CreateScope();
        var fresh = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var balance = (await fresh.AccountBalances.FindAsync(seed.Source))!;
        Assert.Equal(70L, balance.BookBalance);
        Assert.Equal(100L, balance.HeldAmount);
        Assert.Equal(HoldStatus.Active, (await fresh.Holds.FindAsync(seed.Hold))!.Status);
        Assert.False(await fresh.JournalEntries.AnyAsync(x => x.HoldId == seed.Hold));
    }

    [Fact]
    public async Task Failure_after_posting_rolls_back_reservation_journal_and_receipt()
    {
        var seed = await SeedAsync();
        var key = Guid.NewGuid().ToString();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = new CaptureService(db, new FailAfterPosting(new PostingService(db)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CaptureAsync(seed.Hold,
                new CaptureHoldRequest("INTERNAL", seed.Destination, null), key, "capture-test"));
        }
        using var verify = fixture.Factory.Services.CreateScope();
        var fresh = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var balance = (await fresh.AccountBalances.FindAsync(seed.Source))!;
        Assert.Equal(100L, balance.BookBalance);
        Assert.Equal(100L, balance.HeldAmount);
        Assert.Equal(0L, (await fresh.AccountBalances.FindAsync(seed.Destination))!.BookBalance);
        Assert.Equal(HoldStatus.Active, (await fresh.Holds.FindAsync(seed.Hold))!.Status);
        Assert.False(await fresh.JournalEntries.AnyAsync(x => x.HoldId == seed.Hold));
        Assert.False(await fresh.Set<CaptureReceipt>().AnyAsync(x => x.Key == key));
    }

    private class FailAfterPosting(IPostingService real) : IPostingService
    {
        public async Task<JournalEntry> PostAsync(JournalEntry entry, CancellationToken cancellationToken = default)
        {
            await real.PostAsync(entry, cancellationToken);
            throw new InvalidOperationException("Simulated failure before capture is saved");
        }
    }
}
