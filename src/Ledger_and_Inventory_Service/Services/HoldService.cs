using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Npgsql;

namespace Ledger_and_Inventory_Service.Services;

public class HoldService(AppDbContext db,ILogger<HoldService> logger): IHoldService
{
public async Task<HoldResponse> CreateHoldAsync(
    CreateHoldRequest request,
    string correlationId,
    string? idempotencyKey,
    CancellationToken ct)
{

    if (request.Amount.Amount <= 0)
    {
        throw new AppException(
            "ERROR_VALIDATION",
            400,
            "Invalid amount",
            "Hold amount must be greater than zero.");
    }
    
    
    // Check account existence and posting permission.
    var account = await db.LedgerAccounts
        .FirstOrDefaultAsync(
            a => a.Id == request.AccountId,
            ct);

    if (account == null)
    {
        throw new AppException(
            "NOT_FOUND",
            404,
            "Account not found",
            $"Ledger account {request.AccountId} does not exist.");
    }
    
    
    //start transaction
    await using var transaction =
        await db.Database.BeginTransactionAsync(ct);
    
    // Lock the account balance row.
    // Other requests for this account must wait until this transaction ends.
    var accountBalance = await db.AccountBalances
        .FromSqlInterpolated($"""
            SELECT *
            FROM account_balances
            WHERE account_id = {request.AccountId}
            FOR UPDATE
            """)
        .AsTracking()
        .SingleOrDefaultAsync(ct);

    //  Check duplicate transfer.
    var existingHold = await db.Holds
        .FirstOrDefaultAsync(
            h => h.ReferenceId == request.TransferId
                 && h.ReferenceType == "TRANSFER",
            ct);

    if (existingHold != null)
    {
        if (existingHold.AccountId != request.AccountId ||
            existingHold.Amount != request.Amount.Amount ||
            existingHold.Currency != request.Amount.Currency)
        {
            throw new AppException(
                "CONFLICT",
                409,
                "Conflict with existing transfer_id hold",
                "Hold with this transfer_id exists with different parameters.");
        }

        await transaction.CommitAsync(ct);
        return MapToResponse(existingHold);
    }
    
    
    if (!account.PostingAllowed)
    {
        throw new AppException(
            "ACCOUNT_BLOCKED",
            422,
            "Account is blocked",
            "Posting is not allowed on this account.");
    }

    if (account.Currency != request.Amount.Currency)
    {
        throw new AppException(
            "CURRENCY_MISMATCH",
            400,
            "Currency mismatch",
            "Account currency does not match requested currency.");
    }

    //  Check available balance.
    if (accountBalance == null)
    {
        throw new AppException(
            "INSUFFICIENT_FUNDS",
            422,
            "Insufficient funds",
            "Account balance does not exist.");
    }

    var availableBalance =
        accountBalance.BookBalance - accountBalance.HeldAmount;

    if (availableBalance < request.Amount.Amount)
    {
        throw new AppException(
            "INSUFFICIENT_FUNDS",
            422,
            "Insufficient funds",
            "Available balance is lower than the requested amount.");
    }

    //  Create hold.
    var now = DateTimeOffset.UtcNow;
    var expiresInSeconds = request.ExpiresInSeconds ?? 900;

    var hold = new Hold
    {
        Id = Guid.CreateVersion7(),
        AccountId = request.AccountId,
        Amount = request.Amount.Amount,
        Currency = request.Amount.Currency,
        Status = HoldStatus.Active,
        ReferenceType = "TRANSFER",
        ReferenceId = request.TransferId,
        ExpiresAt = now.AddSeconds(expiresInSeconds),
        CreatedAt = now,
        CorrelationId = correlationId
    };

    //  Update reserved balance.
    accountBalance.HeldAmount += request.Amount.Amount;
    accountBalance.Version++;
    accountBalance.UpdatedAt = now;

    db.Holds.Add(hold);

    // Both changes are saved in the same transaction.
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is PostgresException
                  { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        throw new AppException("CONFLICT", 409,
            "Conflict with existing transfer_id hold",
            "Hold with this transfer_id already exists.");
    }

    await transaction.CommitAsync(ct);

    logger.LogInformation(
        "Hold {HoldId} created for account {AccountId} with amount {Amount}",
        hold.Id,
        hold.AccountId,
        hold.Amount);

    return MapToResponse(hold);
}


    public async Task<HoldResponse> GetHoldByIdAsync(Guid holdId, CancellationToken ct)
    {
        var hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct);
        if (hold == null)
        {
            throw new AppException("NOT_FOUND", 404, "Hold not found", $"Hold with id {holdId} does not exist.");
        }

        return MapToResponse(hold);
    }

    private static HoldResponse MapToResponse(Hold hold) => new(
        hold.Id,
        hold.AccountId,
        new MoneyDto(hold.Amount, hold.Currency),
        hold.Status.ToString().ToUpperInvariant(),
        hold.ReferenceId,
        hold.ExpiresAt,
        hold.CapturedEntryId,
        hold.CreatedAt,
        hold.ResolvedAt
    );
}