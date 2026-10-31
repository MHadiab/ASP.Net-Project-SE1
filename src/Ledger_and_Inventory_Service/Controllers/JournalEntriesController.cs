using System.Globalization;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
[Route("api/v1/accounts/{id}/entries")]
public class JournalEntriesController(
    IJournalEntriesService journalEntriesService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(JournalEntriesResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 503)]
    public async Task<ActionResult<JournalEntriesResponse>> Get(
        [FromRoute] string id,
        [FromQuery] string? direction,
        [FromQuery] string? from,
        [FromQuery] string? limit,
        [FromQuery] string? cursor,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken cancellationToken)
    {
        var invalidCorrelationId = correlationId is not null &&
            (string.IsNullOrWhiteSpace(correlationId) ||
             correlationId.Length > 64);

        var responseCorrelationId =
            correlationId is null || invalidCorrelationId
                ? HttpContext.TraceIdentifier
                : correlationId;

        HttpContext.TraceIdentifier = responseCorrelationId;

        Response.OnStarting(() =>
        {
            Response.Headers["X-Correlation-ID"] = responseCorrelationId;
            return Task.CompletedTask;
        });

        if (invalidCorrelationId)
        {
            throw Invalid(
                "X-Correlation-ID must contain between 1 and 64 characters.");
        }

        if (!Guid.TryParseExact(id, "D", out var accountId))
        {
            throw Invalid("id must be a valid UUID.");
        }

        var pageSize = 50;

        if (limit is not null &&
            !int.TryParse(
                limit,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out pageSize))
        {
            throw Invalid("limit must be an integer between 1 and 200.");
        }

        var query = new JournalEntriesQuery(
            direction,
            from,
            pageSize,
            cursor);

        var response = await journalEntriesService.GetAsync(
            accountId,
            query,
            cancellationToken);

        return Ok(response);
    }

    private static AppException Invalid(string detail)
    {
        return new AppException(
            "ERROR_VALIDATION",
            400,
            "Invalid journal entries request",
            detail);
    }
}
