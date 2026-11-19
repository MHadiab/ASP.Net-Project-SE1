namespace Ledger_and_Inventory_Service.DTOs;

// MoneyDto already exists in HoldDto.cs
public record BalanceResponse(
    Guid AccountId,
    MoneyDto Book,
    MoneyDto Held,
    MoneyDto Available,
    DateTimeOffset AsOf
);