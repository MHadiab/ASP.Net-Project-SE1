using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
[Route("api/v1/ledger-accounts/{accountId}/balance")]
public class BalanceController(IBalanceService balances) : ControllerBase
{
    /// <summary>Book, held and available balance of one account.</summary>
    [HttpGet]
    [ProducesResponseType<BalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BalanceResponse>> Get(
        Guid accountId, CancellationToken ct)
    {
        // a balance must never be served from a cache
        Response.Headers.CacheControl = "no-store";
        return Ok(await balances.GetBalanceAsync(accountId, ct));
    }
}
