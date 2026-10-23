namespace Ledger_and_Inventory_Service.Entities;

public enum JournalEntryType
{
    TRANSFER_INTERNAL,
    TRANSFER_EXTERNAL,
    OPENING_BALANCE,
    ADJUSTMENT,
    REVERSAL
}

public enum JournalDirection
{
    DEBIT,
    CREDIT
}