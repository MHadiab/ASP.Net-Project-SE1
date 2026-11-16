using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Ledger_and_Inventory_Service.Services;

public class CaptureService(AppDbContext db, IPostingService posting) : ICaptureService
{
    public async Task<CaptureHoldResponse> CaptureAsync(Guid holdId, CaptureHoldRequest request,
        string idempotencyKey, string correlationId, CancellationToken ct = default)
    {
        if (holdId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 128 || string.IsNullOrWhiteSpace(correlationId)
            || correlationId.Length > 64 || request.TransferType is not ("INTERNAL" or "EXTERNAL")
            || request.Description?.Length > 300
            || (request.TransferType == "INTERNAL"
                && (request.DestinationAccountId is null || request.DestinationAccountId == Guid.Empty)))
            throw new AppException("ERROR_VALIDATION", 400, "Invalid capture request");

        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Capture requires a clean context and owns its transaction.");

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { HoldId = holdId, request.TransferType,
                request.DestinationAccountId, request.Description }))));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize retries of the same key, including keys targeting different holds.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({idempotencyKey}, 0))", ct);
            var receipt = await db.Set<CaptureReceipt>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Key == idempotencyKey, ct);
            if (receipt is not null)
            {
                if (receipt.RequestHash != fingerprint)
                    throw new AppException("IDEMPOTENCY_KEY_REUSED", 409,
                        "Idempotency key reused with a different request");
                var replay = JsonSerializer.Deserialize<CaptureHoldResponse>(receipt.ResponseJson)!;
                await transaction.CommitAsync(ct);
                return replay;
            }

            var hold = (await db.Holds.FromSqlInterpolated($"""
                SELECT h.*, h.xmin FROM holds AS h WHERE h.id = {holdId} FOR UPDATE
                """).AsTracking().ToListAsync(ct)).SingleOrDefault()
                ?? throw new AppException("NOT_FOUND", 404, "Hold not found");

            if (hold.Status == HoldStatus.Captured)
                throw new AppException("HOLD_ALREADY_CAPTURED", 409, "Hold already captured");
            if (hold.Status == HoldStatus.Expired)
                throw new AppException("HOLD_EXPIRED", 409, "Hold has expired");
            if (hold.Status != HoldStatus.Active)
                throw new AppException("HOLD_NOT_ACTIVE", 409, "Hold is not active");
            if (hold.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new AppException("HOLD_EXPIRED", 409, "Hold has expired");

            var destination = request.TransferType == "INTERNAL"
                ? request.DestinationAccountId!.Value
                : await db.LedgerAccounts.AsNoTracking()
                    .Where(x => x.AccountType == AccountType.SYSTEM && x.SystemCode == SystemAccountCode.CLEARING)
                    .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
                    ?? throw new AppException("NOT_FOUND", 404, "CLEARING account not configured");
            if (destination == hold.AccountId)
                throw new AppException("ERROR_VALIDATION", 400, "Source and destination must differ");

            // Use the same order as PostingService: accounts first, then all balances in UUID order.
            // Lock the destination too before releasing the source reservation to avoid lock inversion.
            var accountIds = new[] { hold.AccountId, destination }.OrderBy(x => x).ToArray();
            foreach (var id in accountIds)
            {
                var account = (await db.LedgerAccounts.FromSqlInterpolated($"""
                    SELECT a.*, a.xmin FROM ledger_accounts AS a WHERE a.id = {id} FOR SHARE
                    """).AsNoTracking().ToListAsync(ct)).SingleOrDefault()
                    ?? throw new AppException("NOT_FOUND", 404, "Ledger account not found");
                if (!account.PostingAllowed)
                    throw new AppException("ACCOUNT_BLOCKED", 422, "Account is blocked");
                if (account.Currency != hold.Currency)
                    throw new AppException("CURRENCY_MISMATCH", 400, "Currency mismatch");
                if (account.AccountType != AccountType.CUSTOMER
                    && (id == hold.AccountId || request.TransferType == "INTERNAL"))
                    throw new AppException("ERROR_VALIDATION", 400, "Transfer requires customer accounts");
            }

            AccountBalance? source = null;
            foreach (var id in accountIds)
            {
                var balance = (await db.AccountBalances.FromSqlInterpolated($"""
                    SELECT * FROM account_balances WHERE account_id = {id} FOR UPDATE
                    """).AsTracking().ToListAsync(ct)).SingleOrDefault()
                    ?? throw new AppException("NOT_FOUND", 404, "Account balance not found");
                if (id == hold.AccountId) source = balance;
            }

            // Expiry is checked again after potentially waiting for account locks.
            if (hold.ExpiresAt <= DateTimeOffset.UtcNow)
                throw new AppException("HOLD_EXPIRED", 409, "Hold has expired");
            if (hold.Amount <= 0 || source!.HeldAmount < hold.Amount)
                throw new AppException("CONFLICT", 409, "Hold reservation is inconsistent");

            source.HeldAmount -= hold.Amount;
            await db.SaveChangesAsync(ct); // Still uncommitted; PostingService requires no pending changes.

            var entry = await posting.PostAsync(new JournalEntry
            {
                EntryType = request.TransferType == "INTERNAL"
                    ? JournalEntryType.TRANSFER_INTERNAL : JournalEntryType.TRANSFER_EXTERNAL,
                ReferenceType = "TRANSFER", ReferenceId = hold.ReferenceId, HoldId = hold.Id,
                Currency = hold.Currency, Description = request.Description ?? "",
                CorrelationId = correlationId, CreatedBy = "capture",
                Lines = new List<JournalLine>
                {
                    new() { AccountId = hold.AccountId, Direction = JournalDirection.DEBIT, Amount = hold.Amount },
                    new() { AccountId = destination, Direction = JournalDirection.CREDIT, Amount = hold.Amount }
                }
            }, ct);

            hold.Capture(entry.Id);
            var response = new CaptureHoldResponse(new HoldResponse(hold.Id, hold.AccountId,
                new MoneyDto(hold.Amount, hold.Currency), "CAPTURED", hold.ReferenceId,
                hold.ExpiresAt, hold.CapturedEntryId, hold.CreatedAt, hold.ResolvedAt),
                new JournalEntryResponse(entry.Id, entry.EntryNumber, entry.EntryType.ToString(),
                    entry.ReferenceType, entry.ReferenceId, entry.Description,
                    new MoneyDto(entry.TotalAmount, entry.Currency), entry.PostedAt, entry.BusinessDate,
                    null, false, entry.CorrelationId, entry.Lines.OrderBy(x => x.LineNo)
                        .Select(x => new JournalLineResponse(x.LineNo, x.AccountId, x.Direction.ToString(),
                            x.Amount, x.BalanceAfter)).ToArray()));
            db.Set<CaptureReceipt>().Add(new CaptureReceipt
            {
                Key = idempotencyKey, RequestHash = fingerprint,
                ResponseJson = JsonSerializer.Serialize(response)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return response;
        }
        catch (Exception ex)
        {
            // PostingService also rolls back a caller transaction on posting failure.
            if (transaction.GetDbTransaction().Connection is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            if (ex is NpgsqlException { IsTransient: true })
                throw new AppException("SERVICE_UNAVAILABLE", 503, "Database temporarily unavailable");
            throw;
        }
    }
}
