namespace Ledger_and_Inventory_Service.DTOs;

public record CreateLedgerAccountRequest(
    Guid AccountId,
    string? AccountType,
    string? Currency
);

// Allowed is nullable so a missing field is detected instead of becoming false.
public record UpdateAccountPermissionRequest(
    bool? Allowed,
    string? Reason
);

public record LedgerAccountResponse(
    Guid AccountId,
    string AccountType,
    string Currency,
    bool PostingAllowed,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);