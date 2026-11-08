using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ledger_and_Inventory_Service.Services;

public class LedgerAccountService(
    AppDbContext db,
    ILogger<LedgerAccountService> logger) : ILedgerAccountService
{
    public async Task<LedgerAccountResponse> CreateAsync(
        CreateLedgerAccountRequest request,
        CancellationToken ct)
    {
        // Repeating the same request returns the same account;
        // the same account_id with a different body is a conflict.
        var existing = await FindAsync(request.AccountId, ct);
        if (existing is not null)
            return ReplayOrConflict(existing, request);

        var now = DateTimeOffset.UtcNow;

        // The account and its zero balance row are saved in one transaction.
        var account = new LedgerAccount
        {
            Id = request.AccountId,
            AccountType = AccountType.CUSTOMER,
            Currency = request.Currency!,
            PostingAllowed = true,
            AllowNegative = false,
            CreatedAt = now,
            UpdatedAt = now,
            Balance = new AccountBalance
            {
                AccountId = request.AccountId,
                BookBalance = 0,
                HeldAmount = 0,
                Version = 0,
                UpdatedAt = now
            }
        };

        db.LedgerAccounts.Add(account);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent request created the same account first.
            db.ChangeTracker.Clear();
            var raced = await FindAsync(request.AccountId, ct);
            if (raced is null) throw;
            return ReplayOrConflict(raced, request);
        }

        logger.LogInformation(
            "Ledger account {AccountId} created", account.Id);

        return Map(account);
    }

    public async Task<LedgerAccountResponse> GetAsync(
        Guid accountId,
        CancellationToken ct)
    {
        var account = await FindAsync(accountId, ct)
            ?? throw NotFound(accountId);

        return Map(account);
    }

    public async Task<LedgerAccountResponse> SetPermissionAsync(
        Guid accountId,
        UpdateAccountPermissionRequest request,
        CancellationToken ct)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        // PostingService takes FOR SHARE on this row, so FOR UPDATE here makes the
        // permission change wait for running postings (and the reverse).
        // xmin is a system column and is not included in '*'.
        var rows = await db.LedgerAccounts
            .FromSqlInterpolated($"""
                SELECT account.*, account.xmin
                FROM ledger_accounts AS account
                WHERE account.id = {accountId}
                FOR UPDATE
                """)
            .AsTracking()
            .ToListAsync(ct);

        var account = rows.SingleOrDefault()
            ?? throw NotFound(accountId);

        account.PostingAllowed = request.Allowed!.Value;
        account.Reason = string.IsNullOrWhiteSpace(request.Reason)
            ? null
            : request.Reason.Trim();
        account.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Ledger account {AccountId} posting_allowed set to {PostingAllowed}",
            account.Id, account.PostingAllowed);

        return Map(account);
    }

    private Task<LedgerAccount?> FindAsync(Guid accountId, CancellationToken ct) =>
        db.LedgerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, ct);

    private static LedgerAccountResponse ReplayOrConflict(
        LedgerAccount existing,
        CreateLedgerAccountRequest request)
    {
        var same = existing.AccountType == AccountType.CUSTOMER
                   && existing.Currency == request.Currency;

        if (!same)
            throw new AppException(
                "CONFLICT",
                409,
                "Conflict with existing account",
                "A ledger account with this account_id exists with different parameters.");

        return Map(existing);
    }

    private static AppException NotFound(Guid accountId) =>
        new("NOT_FOUND",
            404,
            "Ledger account not found",
            $"No ledger account exists for {accountId}.");

    private static LedgerAccountResponse Map(LedgerAccount account) => new(
        account.Id,
        account.AccountType.ToString(),
        account.Currency,
        account.PostingAllowed,
        account.CreatedAt,
        account.UpdatedAt);
}