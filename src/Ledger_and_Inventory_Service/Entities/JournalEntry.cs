using System;
using System.Collections.Generic;

namespace Ledger_and_Inventory_Service.Entities;

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public long EntryNumber { get; set; }

    public JournalEntryType EntryType { get; set; }

    public string ReferenceType { get; set; } = string.Empty;

    public Guid? ReferenceId { get; set; }

    public Guid? HoldId { get; set; }

    public Guid? ReversesEntryId { get; set; }

    public string? ReversalReason { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Currency { get; set; } = "IRR";

    public long TotalAmount { get; set; }

    public DateTimeOffset PostedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateOnly BusinessDate { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    public JournalEntry? ReversesEntry { get; set; }

    public JournalEntry? ReversedByEntry { get; set; }

    public ICollection<JournalLine> Lines { get; set; }
        = new List<JournalLine>();
}