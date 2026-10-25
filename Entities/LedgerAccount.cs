namespace Ledger_and_Inventory_Service.Entities;

public class LedgerAccount
{
    // Same value as account_id in S1 (no new id is generated here).
    public Guid Id { get; set; }

    public AccountType AccountType { get; set; }

    // CLEARING / CASH_ISSUANCE for system accounts, null for customers.
    public string? SystemCode { get; set; }

    public string Currency { get; set; } = "IRR";

    public AccountStatus Status { get; set; } = AccountStatus.ACTIVE;

    public bool AllowNegative { get; set; }

    public string? StatusReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AccountBalance Balance { get; set; } = null!;
}
