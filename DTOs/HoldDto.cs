using System;

namespace Ledger_and_Inventory_Service.DTOs;

public record MoneyDto(long Amount, string Currency);

public record CreateHoldRequest(
    Guid AccountId,
    MoneyDto Amount,
    Guid TransferId,
    int? ExpiresInSeconds,
    string? Description
    );
public record HoldResponse(
    Guid HoldId,
    Guid AccountId,
    MoneyDto Amount,
    string Status,
    Guid TransferId,
    DateTimeOffset ExpiresAt,
    Guid? CaptureEntryId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt
    );