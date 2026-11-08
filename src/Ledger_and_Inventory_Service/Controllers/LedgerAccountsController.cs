using System;
using System.Threading;
using System.Threading.Tasks;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Ledger_and_Inventory_Service.Validators;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
[Route("api/v1/ledger-accounts")]
public class LedgerAccountsController(
    ILedgerAccountService accountService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(LedgerAccountResponse), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Create(
        [FromBody] CreateLedgerAccountRequest request,
        // Accepted per GEN-07. Until the shared idempotency table exists, repeats are
        // handled by account_id: same body returns the same account, different body 409.
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        UseCorrelationId(correlationId);

        new CreateLedgerAccountValidator().Validate(request).ThrowIfInvalid();

        var response = await accountService.CreateAsync(request, ct);
        return StatusCode(201, response);
    }

    [HttpGet("{accountId:guid}")]
    [ProducesResponseType(typeof(LedgerAccountResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> Get(
        Guid accountId,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        UseCorrelationId(correlationId);

        var response = await accountService.GetAsync(accountId, ct);
        return Ok(response);
    }

    [HttpPut("{accountId:guid}/permission")]
    [ProducesResponseType(typeof(LedgerAccountResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<IActionResult> SetPermission(
        Guid accountId,
        [FromBody] UpdateAccountPermissionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        UseCorrelationId(correlationId);

        new UpdateAccountPermissionValidator().Validate(request).ThrowIfInvalid();

        // Sets a value, so repeating the call is safe without an idempotency record.
        var response = await accountService.SetPermissionAsync(accountId, request, ct);
        return Ok(response);
    }

    // Same approach as JournalEntriesController: the id is also used in error bodies,
    // and the header is added when the response starts (the exception handler clears headers).
    private void UseCorrelationId(string? correlationId)
    {
        var id = string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64
            ? HttpContext.TraceIdentifier
            : correlationId;

        HttpContext.TraceIdentifier = id;

        Response.OnStarting(() =>
        {
            Response.Headers["X-Correlation-ID"] = id;
            return Task.CompletedTask;
        });
    }
}