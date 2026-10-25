namespace Ledger_and_Inventory_Service.DTOs;

// GET /api/v1/ledger-accounts/{accountId}/balance
public record BalanceResponse(
    Guid AccountId,
    MoneyDto Book,        // sum of posted entries (credit - debit)
    MoneyDto Held,        // sum of ACTIVE holds
    MoneyDto Available,   // Book - Held
    DateTimeOffset AsOf);
