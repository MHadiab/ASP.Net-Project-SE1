using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Services.Interfaces;

public interface IHoldService
{
    Task<HoldResponse> CreateHoldAsync(
        CreateHoldRequest request,
        string correlationId,
        string? idempotencyKey,
        CancellationToken ct
    );

    Task<HoldResponse> GetHoldByIdAsync(
        Guid holdId,
        CancellationToken ct
    );

}