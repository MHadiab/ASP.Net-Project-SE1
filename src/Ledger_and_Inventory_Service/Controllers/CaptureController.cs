using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
[Route("api/v1/holds")]
public class CaptureController(ICaptureService captureService) : ControllerBase
{
    [HttpPost("{holdId:guid}/capture")]
    public async Task<IActionResult> Capture(
        Guid holdId,
        [FromBody] CaptureHoldRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        correlationId ??= HttpContext.TraceIdentifier;
        Response.Headers["X-Correlation-ID"] = correlationId;
        var response = await captureService.CaptureAsync(
            holdId, request, idempotencyKey ?? "", correlationId, ct);
        return Ok(response);
    }
}
