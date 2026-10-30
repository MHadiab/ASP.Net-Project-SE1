using Ledger_and_Inventory_Service.Enums;

namespace Ledger_and_Inventory_Service.Entities;

public class LedgerAccount
{
    // Same value as account_id in S1 (no new id is generated here).
    public Guid Id { get; set; }

    public AccountType AccountType { get; set; }

    // Only for SYSTEM accounts (CLEARING / CASH_ISSUANCE), unique.
    public SystemAccountCode? SystemCode { get; set; }

    public string Currency { get; set; } = "IRR";

    // false means holds and captures on this account are rejected (ACCOUNT_BLOCKED).
    public bool PostingAllowed { get; set; } = true;

    // Only SYSTEM accounts may be true.
    public bool AllowNegative { get; set; }

    // Optional (agreed with S1)
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Optimistic concurrency token (Postgres xmin)
    public uint Xmin { get; set; }

    public AccountBalance Balance { get; set; } = null!;
}
