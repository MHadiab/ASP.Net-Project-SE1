using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Services;

public class BalanceService(AppDbContext db) : IBalanceService
{
    public async Task<BalanceResponse> GetBalanceAsync(
        Guid accountId,
        CancellationToken ct)
    {
        // One row read from the stored projection, so book and held always
        // come from the same committed state (strong consistency).
        // Reads stay allowed even when posting_allowed is false.
        var row = await db.AccountBalances
                      .AsNoTracking()
                      .Where(b => b.AccountId == accountId)
                      .Select(b => new
                      {
                          b.BookBalance,
                          b.HeldAmount,
                          b.Account.Currency
                      })
                      .FirstOrDefaultAsync(ct)
                  ?? throw new AppException(
                      "NOT_FOUND",
                      404,
                      "Ledger account not found",
                      $"No ledger account exists for {accountId}.");

        var currency = row.Currency.Trim();

        return new BalanceResponse(
            accountId,
            new MoneyDto(row.BookBalance, currency),
            new MoneyDto(row.HeldAmount, currency),
            new MoneyDto(row.BookBalance - row.HeldAmount, currency),
            DateTimeOffset.UtcNow);
    }
}