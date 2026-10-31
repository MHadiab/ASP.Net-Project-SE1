namespace Ledger_and_Inventory_Service.DTOs;

public record JournalEntryItemDto(
    Guid EntryId,
    long EntryNumber,
    short LineNo,
    DateTimeOffset PostedAt,
    string Direction,
    long Amount,
    long BalanceAfter,
    string EntryType,
    Guid? ReferenceId,
    string Description,
    bool IsReversed
);

public record JournalEntriesResponse(
    Guid AccountId,
    IReadOnlyList<JournalEntryItemDto> Items,
    string? NextCursor
);

public record JournalEntriesQuery(
    string? Direction = null,
    string? From = null,
    int Limit = 50,
    string? Cursor = null
);
