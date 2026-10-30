namespace Ledger_and_Inventory_Service.Entities;

public class AccountBalance
{
    // PK and FK to LedgerAccount at the same time
    public Guid AccountId { get; set; }

    // sum(credit) - sum(debit), in IRR
    public long BookBalance { get; set; }

    // sum of ACTIVE holds
    public long HeldAmount { get; set; }

    // change counter, for tracing and tests
    public long Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Computed, not stored in the database
    public long AvailableBalance => BookBalance - HeldAmount;

    public LedgerAccount Account { get; set; } = null!;
}