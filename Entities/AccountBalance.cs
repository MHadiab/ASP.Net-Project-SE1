namespace Ledger_and_Inventory_Service.Entities;

// Stored projection used for fast balance reads (ADR-002).
// Only changed in the same transaction that writes a journal line or a hold.
public class AccountBalance
{
    public Guid AccountId { get; set; }

    // sum(CREDIT) - sum(DEBIT) of posted journal lines, in IRR.
    public long BalanceBook { get; set; }

    // sum of ACTIVE holds on this account.
    public long HeldAmount { get; set; }

    public long Version { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public LedgerAccount Account { get; set; } = null!;
}
