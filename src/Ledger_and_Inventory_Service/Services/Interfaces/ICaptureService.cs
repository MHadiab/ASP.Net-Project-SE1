using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Services.Interfaces;

public interface ICaptureService
{
    Task<CaptureHoldResponse> CaptureAsync(
        Guid holdId,
        CaptureHoldRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken ct = default);
}
