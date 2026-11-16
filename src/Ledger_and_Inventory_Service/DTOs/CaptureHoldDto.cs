namespace Ledger_and_Inventory_Service.DTOs;

public record CaptureHoldRequest(string TransferType, Guid? DestinationAccountId, string? Description);
public record CaptureHoldResponse(HoldResponse Hold, JournalEntryResponse Entry);
public record JournalLineResponse(short LineNo, Guid AccountId, string Direction, long Amount, long BalanceAfter);
public record JournalEntryResponse(
    Guid EntryId, long EntryNumber, string EntryType, string ReferenceType,
    Guid? ReferenceId, string Description, MoneyDto Total, DateTimeOffset PostedAt,
    DateOnly BusinessDate, Guid? ReversesEntryId, bool IsReversed, string CorrelationId,
    IReadOnlyList<JournalLineResponse> Lines);
