using FluentValidation;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
[Route("api/v1/holds")]
public class HoldsController(IHoldService holdService,
    IValidator<CreateHoldRequest> createValidator,
    IValidator<ReleaseHoldRequest> releaseValidator) : ControllerBase
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
        
        await createValidator.ValidateAndThrowAsync(request, ct);
        
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
    
    [HttpPost("{holdId:guid}/release")]
    public async Task<IActionResult> ReleaseHold(
        Guid holdId,
        [FromBody] ReleaseHoldRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        correlationId ??= HttpContext.TraceIdentifier;
        HttpContext.Response.Headers["X-Correlation-ID"] = correlationId;

        if (request is not null)
            await releaseValidator.ValidateAndThrowAsync(request, ct);

        var response = await holdService.ReleaseHoldAsync(holdId, request, correlationId, ct);
        return Ok(response);
    }
    
    
}


