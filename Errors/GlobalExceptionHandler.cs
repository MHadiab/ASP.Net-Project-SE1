namespace Ledger_and_Inventory_Service.Errors;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
public class GlobalExceptionHandler(
    IProblemDetailsService problems,
    ILogger<GlobalExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, code, title) = ex is AppException a
            ? (a.Status, a.Code, a.Title)
            : (500, "INTERNAL_ERROR", "Unexpected error");
        if (status >= 500) log.LogError(ex, "Unhandled exception");
        ctx.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                Type = "https://errors.labbank.example/"
                       + code.ToLower().Replace('_', '-'),
                Detail = ex is AppException ? ex.Message : null,
                Extensions =
                {
                    ["code"] = code,
                    ["correlation_id"] = ctx.TraceIdentifier
                }
            }
        });
    }
}
