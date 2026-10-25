namespace Ledger_and_Inventory_Service.DTOs;

// Shared money shape (GEN-03): {"amount":2500000,"currency":"IRR"}
public record MoneyDto(long Amount, string Currency);
