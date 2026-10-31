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

    [Theory]
    [InlineData(0L)]
    [InlineData(20L)]
    public async Task Capture_posts_reserved_amount_and_preserves_other_holds_in_callers_transaction(
        long otherHeldAmount)
    {
        var (source, destination) = await SeedAsync();
        var holdId = await ReserveAsync(source, 80, otherHeldAmount);
        await using var db = CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockCaptureAccountsAsync(db, source, destination);
        var hold = await db.Holds.SingleAsync(row => row.Id == holdId);
        var sourceBalance = await db.AccountBalances.SingleAsync(row => row.AccountId == source);
        sourceBalance.HeldAmount -= hold.Amount;
        await db.SaveChangesAsync();
        Assert.False(db.ChangeTracker.HasChanges());

        var entry = Transfer(source, destination, hold.Amount);
        entry.HoldId = hold.Id;
        entry.ReferenceId = hold.ReferenceId;
        await new PostingService(db).PostAsync(entry);
        hold.Capture(entry.Id);
        await db.SaveChangesAsync();

        await using (var outside = CreateContext())
        {
            var outsideBalance = (await outside.AccountBalances.FindAsync(source))!;
            Assert.Equal(100L, outsideBalance.BookBalance);
            Assert.Equal(80L + otherHeldAmount, outsideBalance.HeldAmount);
            Assert.Equal(HoldStatus.Active, (await outside.Holds.FindAsync(holdId))!.Status);
            Assert.Equal(1, await outside.JournalEntries.CountAsync());
        }

        await transaction.CommitAsync();

        await using var verify = CreateContext();
        var persistedBalance = (await verify.AccountBalances.FindAsync(source))!;
        var persistedHold = (await verify.Holds.FindAsync(holdId))!;
        Assert.Equal(20L, persistedBalance.BookBalance);
        Assert.Equal(otherHeldAmount, persistedBalance.HeldAmount);
        Assert.Equal(20L - otherHeldAmount, persistedBalance.AvailableBalance);
        Assert.Equal(80L, (await verify.AccountBalances.FindAsync(destination))!.BookBalance);
        Assert.Equal(HoldStatus.Captured, persistedHold.Status);
        Assert.Equal(entry.Id, persistedHold.CapturedEntryId);
        Assert.NotNull(persistedHold.ResolvedAt);
        Assert.Equal(holdId, (await verify.JournalEntries.FindAsync(entry.Id))!.HoldId);
        Assert.Equal(2, await verify.JournalEntries.CountAsync());
        Assert.Equal(4, await verify.JournalLines.CountAsync());
        Assert.Equal(0L, await verify.AccountBalances.SumAsync(balance => balance.BookBalance));
        if (otherHeldAmount > 0)
        {
            var remainingHold = await verify.Holds.SingleAsync(row =>
                row.AccountId == source && row.Id != holdId);
            Assert.Equal(HoldStatus.Active, remainingHold.Status);
            Assert.Equal(otherHeldAmount, remainingHold.Amount);
        }
    }

    [Fact]
    public async Task Capture_journal_failure_restores_reservation_and_tracked_hold_changes()
    {
        var (source, destination) = await SeedAsync();
        var holdId = await ReserveAsync(source, 80, 20);
        await using var db = CreateContext();
        var existingEntryId = await db.JournalEntries.AsNoTracking()
            .Select(entry => entry.Id).SingleAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await LockCaptureAccountsAsync(db, source, destination);
        var hold = await db.Holds.SingleAsync(row => row.Id == holdId);
        var sourceBalance = await db.AccountBalances.SingleAsync(row => row.AccountId == source);
        var duplicate = Transfer(source, destination, hold.Amount);
        duplicate.Id = existingEntryId;
        duplicate.HoldId = hold.Id;
        duplicate.ReferenceId = hold.ReferenceId;
        sourceBalance.HeldAmount -= hold.Amount;
        hold.Capture(duplicate.Id);
        Assert.True(db.ChangeTracker.HasChanges());
        await db.SaveChangesAsync();
        Assert.False(db.ChangeTracker.HasChanges());

        var error = await Assert.ThrowsAsync<AppException>(() =>
            new PostingService(db).PostAsync(duplicate));

        Assert.Equal("CONFLICT", error.Code);
        await using var verify = CreateContext();
        var persistedBalance = (await verify.AccountBalances.FindAsync(source))!;
        var persistedHold = (await verify.Holds.FindAsync(holdId))!;
        Assert.Equal(100L, persistedBalance.BookBalance);
        Assert.Equal(100L, persistedBalance.HeldAmount);
        Assert.Equal(0L, persistedBalance.AvailableBalance);
        Assert.Equal(1L, persistedBalance.Version);
        var destinationBalance = (await verify.AccountBalances.FindAsync(destination))!;
        Assert.Equal(0L, destinationBalance.BookBalance);
        Assert.Equal(0L, destinationBalance.Version);
        Assert.Equal(HoldStatus.Active, persistedHold.Status);
        Assert.Null(persistedHold.CapturedEntryId);
        Assert.Null(persistedHold.ResolvedAt);
        Assert.Equal(2, await verify.Holds.CountAsync(row => row.Status == HoldStatus.Active));
        Assert.Equal(1, await verify.JournalEntries.CountAsync());
        Assert.Equal(2, await verify.JournalLines.CountAsync());
    }

    private async Task<Guid> ReserveAsync(Guid accountId, long amount, long otherHeldAmount)
    {
        await using var db = CreateContext();
        var balance = await db.AccountBalances.SingleAsync(row => row.AccountId == accountId);
        balance.HeldAmount = amount + otherHeldAmount;
        var hold = new Hold
        {
            AccountId = accountId,
            Amount = amount,
            ReferenceId = Guid.CreateVersion7(),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            CorrelationId = "capture-posting-tests"
        };
        db.Holds.Add(hold);
        if (otherHeldAmount > 0)
        {
            db.Holds.Add(new Hold
            {
                AccountId = accountId,
                Amount = otherHeldAmount,
                ReferenceId = Guid.CreateVersion7(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                CorrelationId = "other-hold-tests"
            });
        }
        await db.SaveChangesAsync();
        return hold.Id;
    }

    private static async Task LockCaptureAccountsAsync(AppDbContext db, params Guid[] accountIds)
    {
        var orderedIds = accountIds.Distinct().OrderBy(accountId => accountId).ToArray();
        foreach (var accountId in orderedIds)
        {
            await db.LedgerAccounts.FromSqlInterpolated($"""
                SELECT account.*, account.xmin FROM ledger_accounts AS account
                WHERE account.id = {accountId} FOR SHARE
                """).AsNoTracking().ToListAsync();
        }
        foreach (var accountId in orderedIds)
        {
            await db.AccountBalances.FromSqlInterpolated($"""
                SELECT * FROM account_balances
                WHERE account_id = {accountId} FOR UPDATE
                """).AsNoTracking().ToListAsync();
        }
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
