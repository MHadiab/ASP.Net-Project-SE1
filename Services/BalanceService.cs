using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Services;

public class BalanceService(AppDbContext db) : IBalanceService
{
    public async Task<BalanceResponse> GetBalanceAsync(
        Guid accountId, CancellationToken ct)
    {
        var row = await db.AccountBalances
            .AsNoTracking()
            .Where(b => b.AccountId == accountId)
            .Select(b => new
            {
                b.BalanceBook,
                b.HeldAmount,
                b.Account.Currency,
                b.UpdatedAt
            })
            .SingleOrDefaultAsync(ct);

        if (row is null)
            throw new AppException(
                "NOT_FOUND",
                404,
                "Account not found",
                $"Ledger account {accountId} does not exist.");

        var currency = row.Currency.Trim();

        return new BalanceResponse(
            accountId,
            Book: new MoneyDto(row.BalanceBook, currency),
            Held: new MoneyDto(row.HeldAmount, currency),
            Available: new MoneyDto(
                row.BalanceBook - row.HeldAmount, currency),
            AsOf: row.UpdatedAt);
    }
}