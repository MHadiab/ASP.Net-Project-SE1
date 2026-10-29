using System;
using System.Threading;
using System.Threading.Tasks;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
[Route("api/v1/holds")]
public class HoldsController(IHoldService holdService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateHold(
        [FromBody] CreateHoldRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        correlationId ??= HttpContext.TraceIdentifier;
        HttpContext.Response.Headers["X-Correlation-ID"] = correlationId;

        var response = await holdService.CreateHoldAsync(request, correlationId, idempotencyKey, ct);
        return StatusCode(201, response);
    }

    [HttpGet("{holdId:guid}")]
    public async Task<IActionResult> GetHold(
        Guid holdId,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        correlationId ??= HttpContext.TraceIdentifier;
        HttpContext.Response.Headers["X-Correlation-ID"] = correlationId;

        var response = await holdService.GetHoldByIdAsync(holdId, ct);
        return Ok(response);
    }
}