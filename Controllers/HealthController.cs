using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Ledger_and_Inventory_Service.Controllers;

[ApiController]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "ok" });
    [HttpGet("/ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        if (!await db.Database.CanConnectAsync(ct))
            throw new AppException("SERVICE_UNAVAILABLE", 503,
                "Database unavailable");
        return Ok(new { status = "ready" });
    }
}
