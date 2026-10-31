using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ledger.Tests;

public class PostingServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Posts_entry_and_updates_balances_and_line_history()
    {
        var (source, destination) = await SeedAsync();
        await using var db = CreateContext();
        var entry = Transfer(source, destination, 30);

        var result = await new PostingService(db).PostAsync(entry);

        Assert.True(result.EntryNumber > 0);
        Assert.Equal(30L, result.TotalAmount);
        Assert.Equal(TimeSpan.Zero, result.PostedAt.Offset);
        Assert.Equal(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(result.PostedAt,
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")).DateTime), result.BusinessDate);
        var lines = result.Lines.OrderBy(line => line.LineNo).ToArray();
        Assert.Equal(70L, lines[0].BalanceAfter);
        Assert.Equal(30L, lines[1].BalanceAfter);
        Assert.All(lines, line => Assert.Equal(result.PostedAt, line.PostedAt));

        await using var verify = CreateContext();
        Assert.Equal(70L, (await verify.AccountBalances.FindAsync(source))!.BookBalance);
        Assert.Equal(30L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(2L, (await verify.AccountBalances.FindAsync(source))!.Version);
        Assert.Equal(1L, (await verify.AccountBalances.FindAsync(destination))!.Version);
        Assert.Equal(2, await verify.JournalEntries.CountAsync());
        Assert.Equal(0L, await verify.AccountBalances.SumAsync(balance => balance.BookBalance));
    }

    [Fact]
    public async Task Reserved_funds_cannot_be_spent_by_direct_posting()
    {
        var (source, destination) = await SeedAsync();
        await using (var setup = CreateContext())
        {
            var balance = await setup.AccountBalances.SingleAsync(row => row.AccountId == source);
            balance.HeldAmount = 80;
            setup.Holds.Add(new Hold
            {
                AccountId = source,
                Amount = 80,
                ReferenceId = Guid.CreateVersion7(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                CorrelationId = "posting-tests"
            });
            await setup.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var error = await Assert.ThrowsAsync<AppException>(() =>
            new PostingService(db).PostAsync(Transfer(source, destination, 30)));

        Assert.Equal("INSUFFICIENT_FUNDS", error.Code);
        await using var verify = CreateContext();
        var sourceBalance = (await verify.AccountBalances.FindAsync(source))!;
        Assert.Equal(100L, sourceBalance.BookBalance);
        Assert.Equal(80L, sourceBalance.HeldAmount);
        Assert.Equal(0L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Concurrent_postings_cannot_spend_the_same_funds()
    {
        var (source, destination) = await SeedAsync();

        async Task<bool> TryPostAsync()
        {
            await using var db = CreateContext();
            try
            {
                await new PostingService(db).PostAsync(Transfer(source, destination, 80));
                return true;
            }
            catch (AppException error) when (error.Code == "INSUFFICIENT_FUNDS")
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryPostAsync(), TryPostAsync());

        Assert.Single(results, succeeded => succeeded);
        await using var verify = CreateContext();
        Assert.Equal(20L, (await verify.AccountBalances.FindAsync(source))!.BookBalance);
        Assert.Equal(80L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(2, await verify.JournalEntries.CountAsync());
        Assert.Equal(0L, await verify.AccountBalances.SumAsync(balance => balance.BookBalance));
    }

    [Fact]
    public async Task Journal_insert_failure_rolls_back_preceding_balance_updates()
    {
        var (source, destination) = await SeedAsync();
        await using var db = CreateContext();
        var existingId = await db.JournalEntries.AsNoTracking()
            .Select(entry => entry.Id).SingleAsync();
        var duplicate = Transfer(source, destination, 30);
        duplicate.Id = existingId;

        var error = await Assert.ThrowsAsync<AppException>(() =>
            new PostingService(db).PostAsync(duplicate));

        Assert.Equal("CONFLICT", error.Code);
        await using var verify = CreateContext();
        var sourceBalance = (await verify.AccountBalances.FindAsync(source))!;
        Assert.Equal(100L, sourceBalance.BookBalance);
        Assert.Equal(1L, sourceBalance.Version);
        Assert.Equal(0L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
        Assert.Equal(2, await verify.JournalLines.CountAsync());
    }

    [Fact]
    public async Task Caller_owns_commit_when_a_transaction_is_already_open()
    {
        var (source, destination) = await SeedAsync();
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await new PostingService(db).PostAsync(Transfer(source, destination, 30));

        await using (var outside = CreateContext())
        {
            Assert.Equal(100L, (await outside.AccountBalances.FindAsync(source))!.BookBalance);
            Assert.Equal(1, await outside.JournalEntries.CountAsync());
        }

        await transaction.RollbackAsync();
        await using var verify = CreateContext();
        Assert.Equal(100L, (await verify.AccountBalances.FindAsync(source))!.BookBalance);
        Assert.Equal(0L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Late_failure_also_rolls_back_the_callers_transaction()
    {
        var (source, destination) = await SeedAsync();
        await using var db = CreateContext();
        var existingId = await db.JournalEntries.AsNoTracking()
            .Select(entry => entry.Id).SingleAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.LedgerAccounts.Where(account => account.Id == source)
            .ExecuteUpdateAsync(setters => setters.SetProperty(account => account.Reason, "outer-change"));
        var duplicate = Transfer(source, destination, 30);
        duplicate.Id = existingId;

        var error = await Assert.ThrowsAsync<AppException>(() =>
            new PostingService(db).PostAsync(duplicate));

        Assert.Equal("CONFLICT", error.Code);
        await using var verify = CreateContext();
        Assert.Null((await verify.LedgerAccounts.FindAsync(source))!.Reason);
        Assert.Equal(100L, (await verify.AccountBalances.FindAsync(source))!.BookBalance);
        Assert.Equal(0L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Repeated_account_lines_keep_the_running_balance_and_one_version_increment()
    {
        var (source, destination) = await SeedAsync();
        await using var db = CreateContext();
        var entry = Transfer(source, destination, 10);
        entry.Lines.Last().Amount = 5;
        entry.Lines.Add(new JournalLine
        {
            AccountId = destination, Direction = JournalDirection.CREDIT, Amount = 5
        });

        await new PostingService(db).PostAsync(entry);

        Assert.Equal(new long[] { 90, 5, 10 }, entry.Lines.Select(line => line.BalanceAfter));
        Assert.Equal(new short[] { 1, 2, 3 }, entry.Lines.Select(line => line.LineNo));
        await using var verify = CreateContext();
        Assert.Equal(1L, (await verify.AccountBalances.FindAsync(destination))!.Version);
    }

    [Fact]
    public async Task Previously_tracked_balances_are_refreshed_after_posting()
    {
        var (source, destination) = await SeedAsync();
        await using var db = CreateContext();
        var trackedSource = (await db.AccountBalances.FindAsync(source))!;
        var trackedDestination = (await db.AccountBalances.FindAsync(destination))!;

        await new PostingService(db).PostAsync(Transfer(source, destination, 30));

        Assert.Equal(70L, trackedSource.BookBalance);
        Assert.Equal(30L, trackedDestination.BookBalance);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private async Task<(Guid Source, Guid Destination)> SeedAsync()
    {
        var issuance = NewAccount(AccountType.SYSTEM);
        issuance.SystemCode = SystemAccountCode.CASH_ISSUANCE;
        issuance.AllowNegative = true;
        var source = NewAccount(AccountType.CUSTOMER);
        var destination = NewAccount(AccountType.CUSTOMER);

        await using var db = CreateContext();
        db.LedgerAccounts.AddRange(issuance, source, destination);
        await db.SaveChangesAsync();
        var opening = Transfer(issuance.Id, source.Id, 100);
        opening.EntryType = JournalEntryType.OPENING_BALANCE;
        opening.ReferenceType = "MANUAL";
        await new PostingService(db).PostAsync(opening);
        return (source.Id, destination.Id);
    }

    private static LedgerAccount NewAccount(AccountType type)
    {
        var id = Guid.CreateVersion7();
        return new LedgerAccount
        {
            Id = id, AccountType = type,
            Balance = new AccountBalance { AccountId = id }
        };
    }

    private static JournalEntry Transfer(Guid source, Guid destination, long amount) => new()
    {
        EntryType = JournalEntryType.TRANSFER_INTERNAL,
        ReferenceType = "TRANSFER",
        ReferenceId = Guid.CreateVersion7(),
        Description = "Posting service integration test",
        CorrelationId = "posting-tests",
        CreatedBy = "Ledger.Tests",
        Lines = new List<JournalLine>
        {
            new() { AccountId = source, Direction = JournalDirection.DEBIT, Amount = amount },
            new() { AccountId = destination, Direction = JournalDirection.CREDIT, Amount = amount }
        }
    };
}
