using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Services.Interfaces;

public interface ILedgerAccountService
{
    Task<LedgerAccountResponse> CreateAsync(
        CreateLedgerAccountRequest request,
        CancellationToken ct);

    Task<LedgerAccountResponse> GetAsync(
        Guid accountId,
        CancellationToken ct);

    Task<LedgerAccountResponse> SetPermissionAsync(
        Guid accountId,
        UpdateAccountPermissionRequest request,
        CancellationToken ct);
}