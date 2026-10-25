namespace Ledger_and_Inventory_Service.Entities;

public class JournalLine
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EntryId { get; set; }

    public short LineNo { get; set; }

    public Guid AccountId { get; set; }

    public JournalDirection Direction { get; set; }

    public long Amount { get; set; }

    public long BalanceAfter { get; set; }

    public DateTimeOffset PostedAt { get; set; }

    public JournalEntry Entry { get; set; } = null!;
}