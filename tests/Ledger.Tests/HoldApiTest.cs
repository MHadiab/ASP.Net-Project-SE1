using System.Net;
using System.Text;
using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

public class HoldApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("ledger_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Setup: Start PostgreSQL and API
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:Default"] =
                                _postgres.GetConnectionString()
                        });
                });
            });

        _client = _factory.CreateClient();

        // Apply database migrations
        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        
    }

    // Cleanup
    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();

        await _postgres.DisposeAsync();
    }
    
    // Seed account for successful API request
    private async Task<Guid> SeedAccountAsync()
    {
        var accountId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var account = new LedgerAccount
        {
            Id = accountId,
            AccountType = AccountType.CUSTOMER,
            SystemCode = null,
            Currency = "IRR",
            PostingAllowed = true,
            AllowNegative = false
        };

        var balance = new AccountBalance
        {
            AccountId = accountId,
            BookBalance = 100_000,
            HeldAmount = 0,
            Version = 1
        };

        db.LedgerAccounts.Add(account);
        db.AccountBalances.Add(balance);

        await db.SaveChangesAsync();

        return accountId;
    }

    // Create HTTP request
    private static HttpRequestMessage CreateHoldRequest(
        Guid accountId,
        Guid transferId)
    {
        var body = new
        {
            account_id = accountId,
            amount = new
            {
                amount = 5000,
                currency = "IRR"
            },
            transfer_id = transferId,
            expires_in_seconds = 3600,
            description = "API integration test"
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/holds");

        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        request.Headers.Add(
            "Idempotency-Key",
            $"{transferId}:hold");

        request.Headers.Add(
            "X-Correlation-ID",
            Guid.NewGuid().ToString());

        return request;
    }

    private static void AssertHoldResponseContract(JsonElement root)
    {
        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        Assert.True(root.TryGetProperty("account_id", out var accountId));
        Assert.Equal(JsonValueKind.String, accountId.ValueKind);
        Assert.True(Guid.TryParse(accountId.GetString(), out _));

        Assert.True(root.TryGetProperty("transfer_id", out var transferId));
        Assert.Equal(JsonValueKind.String, transferId.ValueKind);
        Assert.True(Guid.TryParse(transferId.GetString(), out _));

        Assert.True(root.TryGetProperty("amount", out var amount));
        Assert.Equal(JsonValueKind.Object, amount.ValueKind);

        Assert.True(amount.TryGetProperty("amount", out var amountValue));
        Assert.Equal(JsonValueKind.Number, amountValue.ValueKind);

        Assert.True(amount.TryGetProperty("currency", out var currency));
        Assert.Equal(JsonValueKind.String, currency.ValueKind);

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(JsonValueKind.String, status.ValueKind);
    }

    private static void AssertProblemDetailsContract(
        JsonElement root,
        int expectedStatus)
    {
        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        Assert.True(root.TryGetProperty("type", out var type));
        Assert.Equal(JsonValueKind.String, type.ValueKind);

        Assert.True(root.TryGetProperty("title", out var title));
        Assert.Equal(JsonValueKind.String, title.ValueKind);

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal(expectedStatus, status.GetInt32());

        Assert.True(root.TryGetProperty("code", out var code));
        Assert.Equal(JsonValueKind.String, code.ValueKind);

        Assert.True(root.TryGetProperty("correlation_id", out var correlationId));
        Assert.Equal(JsonValueKind.String, correlationId.ValueKind);
    }

    // TEST 1: Successful Hold creation
    [Fact]
    public async Task CreateHold_ShouldReturn201()
    {
        var accountId = await SeedAccountAsync();
        var transferId = Guid.NewGuid();

        using var request = CreateHoldRequest(
            accountId,
            transferId);

        using var response = await _client.SendAsync(request);

        // HTTP status
        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        // Content-Type
        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        // Check response values
        Assert.Equal(
            accountId.ToString(),
            root.GetProperty("account_id").GetString());

        Assert.Equal(
            transferId.ToString(),
            root.GetProperty("transfer_id").GetString());

        Assert.Equal(
            5000,
            root.GetProperty("amount")
                .GetProperty("amount").GetInt64());

        Assert.Equal(
            "IRR",
            root.GetProperty("amount")
                .GetProperty("currency").GetString());

        Assert.Equal(
            "ACTIVE",
            root.GetProperty("status").GetString());

        AssertHoldResponseContract(root);
    }

    // TEST 2: Non-existing account -> Problem Details
    [Fact]
    public async Task CreateHold_WithNonExistingAccount_ShouldReturn404ProblemDetails()
    {
        var accountId = Guid.NewGuid();
        var transferId = Guid.NewGuid();

        using var request = CreateHoldRequest(
            accountId,
            transferId);

        using var response = await _client.SendAsync(request);

        // HTTP status
        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        // Content-Type
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;
        
        
        // Check Problem Details fields
        Assert.Equal(
            404,
            root.GetProperty("status").GetInt32());

        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("type").GetString()));

        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("title").GetString()));

        Assert.False(string.IsNullOrWhiteSpace(
            root.GetProperty("code").GetString()));

        AssertProblemDetailsContract(root, 404);
    }
}
