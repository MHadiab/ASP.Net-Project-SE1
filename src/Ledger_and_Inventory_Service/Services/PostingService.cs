using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Ledger_and_Inventory_Service.Validators;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ledger_and_Inventory_Service.Services;

public class PostingService(AppDbContext db) : IPostingService
{
    public async Task<JournalEntry> PostAsync(
        JournalEntry entry,
        CancellationToken cancellationToken = default)
    {
        var total = JournalEntryValidator.ValidateAndGetTotal(entry);
        ValidateMetadata(entry);

        // SaveChanges below must only save the journal being posted.
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException(
                "Save or discard pending tracked changes before posting.");

        var lines = entry.Lines.ToArray();
        var accountIds = lines.Select(line => line.AccountId)
            .Distinct().OrderBy(id => id).ToArray();

        // A caller such as Capture may own a larger transaction.
        var existingTransaction = db.Database.CurrentTransaction;
        await using var ownedTransaction = existingTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var transaction = existingTransaction ?? ownedTransaction!;

        try
        {
            // Keep account permissions stable while the balances are changed.
            var accounts = await LockAccountsAsync(accountIds, cancellationToken);
            var balances = await LockBalancesAsync(accountIds, cancellationToken);

            var now = DateTimeOffset.UtcNow;
            entry.Id = entry.Id == Guid.Empty ? Guid.CreateVersion7() : entry.Id;
            entry.EntryNumber = 0; // PostgreSQL generates the journal number.
            entry.TotalAmount = total;
            entry.PostedAt = now;
            entry.BusinessDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(now,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran")).DateTime);

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var account = accounts[line.AccountId];
                var balance = balances[line.AccountId];

                if (account.Currency != entry.Currency)
                    throw new AppException("CURRENCY_MISMATCH", 400,
                        "Account currency does not match the journal currency");

                if (!account.PostingAllowed && entry.EntryType != JournalEntryType.REVERSAL)
                    throw new AppException("ACCOUNT_BLOCKED", 422,
                        "Posting is not allowed on this account");

                var newBookBalance = line.Direction == JournalDirection.CREDIT
                    ? checked(balance.BookBalance + line.Amount)
                    : checked(balance.BookBalance - line.Amount);

                var allowsNegative = account.AccountType == AccountType.SYSTEM
                                     && account.AllowNegative;
                if (!allowsNegative && newBookBalance < balance.HeldAmount)
                    throw new AppException("INSUFFICIENT_FUNDS", 422,
                        "Insufficient available balance",
                        $"Posting would consume reserved or unavailable funds on account {account.Id}.");

                balance.BookBalance = newBookBalance;
                line.Id = line.Id == Guid.Empty ? Guid.CreateVersion7() : line.Id;
                line.EntryId = entry.Id;
                line.Entry = entry;
                line.LineNo = checked((short)(index + 1));
                line.PostedAt = now;
                line.BalanceAfter = newBookBalance;
            }

            foreach (var accountId in accountIds)
            {
                var balance = balances[accountId];
                balance.Version = checked(balance.Version + 1);
                balance.UpdatedAt = now;

                // The locked balances are detached, so update them explicitly.
                var affected = await db.AccountBalances
                    .Where(row => row.AccountId == accountId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(row => row.BookBalance, balance.BookBalance)
                        .SetProperty(row => row.Version, balance.Version)
                        .SetProperty(row => row.UpdatedAt, balance.UpdatedAt),
                        cancellationToken);

                if (affected != 1)
                    throw new AppException("CONFLICT", 409,
                        "Account balance changed unexpectedly");
            }

            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);

            // Keep any previously tracked, unchanged balance projections current.
            foreach (var tracked in db.ChangeTracker.Entries<AccountBalance>().ToArray())
            {
                if (!balances.TryGetValue(tracked.Entity.AccountId, out var balance))
                    continue;
                tracked.CurrentValues.SetValues(balance);
                tracked.OriginalValues.SetValues(balance);
                tracked.State = EntityState.Unchanged;
            }

            if (ownedTransaction is not null)
                await ownedTransaction.CommitAsync(cancellationToken);

            return entry;
        }
        catch (Exception exception)
        {
            // Failure also aborts a caller-owned transaction: no partial posting.
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var line in lines)
                db.Entry(line).State = EntityState.Detached;
            db.Entry(entry).State = EntityState.Detached;

            if (exception is OverflowException)
                throw new AppException("ERROR_VALIDATION", 400,
                    "The resulting balance or version exceeds the supported range");

            if (exception is DbUpdateException
                { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
                throw new AppException("CONFLICT", 409,
                    "A journal identifier or reversal already exists");

            throw;
        }
    }

    private static void ValidateMetadata(JournalEntry entry)
    {
        if (!Enum.IsDefined(entry.EntryType)
            || entry.ReferenceType is not ("TRANSFER" or "HOLD" or "MANUAL")
            || entry.Description is null || entry.Description.Length > 300
            || string.IsNullOrWhiteSpace(entry.CorrelationId) || entry.CorrelationId.Length > 64
            || string.IsNullOrWhiteSpace(entry.CreatedBy) || entry.CreatedBy.Length > 64
            || entry.Lines.Count > short.MaxValue
            || entry.Lines.Any(line => line.AccountId == Guid.Empty))
            throw new AppException("ERROR_VALIDATION", 400,
                "Invalid journal metadata");

        if (entry.EntryType == JournalEntryType.REVERSAL)
        {
            if (entry.ReversesEntryId is null || entry.ReversesEntryId == Guid.Empty
                || entry.ReversesEntryId == entry.Id
                || string.IsNullOrWhiteSpace(entry.ReversalReason)
                || entry.ReversalReason.Length > 300)
                throw new AppException("ERROR_VALIDATION", 400,
                    "A reversal requires the original entry and a reason");
        }
        else if (entry.ReversesEntryId is not null || entry.ReversalReason is not null)
        {
            throw new AppException("ERROR_VALIDATION", 400,
                "Only reversal entries may specify reversal fields");
        }
    }

    private async Task<Dictionary<Guid, LedgerAccount>> LockAccountsAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        var accounts = new Dictionary<Guid, LedgerAccount>();
        foreach (var accountId in accountIds)
        {
            // xmin is a PostgreSQL system column and is not included in '*'.
            var rows = await db.LedgerAccounts
                .FromSqlInterpolated($"""
                    SELECT account.*, account.xmin
                    FROM ledger_accounts AS account
                    WHERE account.id = {accountId}
                    FOR SHARE
                    """)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var account = rows.SingleOrDefault();
            if (account is null)
                throw new AppException("NOT_FOUND", 404, "Ledger account not found",
                    $"No ledger account exists for {accountId}.");

            accounts.Add(accountId, account);
        }

        return accounts;
    }

    private async Task<Dictionary<Guid, AccountBalance>>
        LockBalancesAsync(
            IEnumerable<Guid> accountIds,
            CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Balance locks require an active transaction.");
        }

        var orderedIds = accountIds
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        var balances = new Dictionary<Guid, AccountBalance>();

        foreach (var accountId in orderedIds)
        {
            var rows = await db.AccountBalances
                .FromSqlInterpolated($"""
                                      SELECT *
                                      FROM account_balances
                                      WHERE account_id = {accountId}
                                      FOR UPDATE
                                      """)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var balance = rows.SingleOrDefault();

            if (balance is null)
            {
                throw new AppException(
                    "NOT_FOUND", 404,
                    "Account balance not found",
                    $"No balance exists for account {accountId}.");
            }

            balances.Add(accountId, balance);
        }

        return balances;
    }
}
