using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Services;

public interface IBalanceService
{
    Task<BalanceResponse> GetBalanceAsync(
        Guid accountId, CancellationToken ct);
}
