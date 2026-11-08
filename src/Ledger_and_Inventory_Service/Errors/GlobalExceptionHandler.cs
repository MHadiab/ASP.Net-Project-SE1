namespace Ledger_and_Inventory_Service.Errors;

using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class GlobalExceptionHandler(
    IProblemDetailsService problems,
    ILogger<GlobalExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var correlationId =
            ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? ctx.TraceIdentifier;

        object? errors = null;
        string? detail = null;

        int status;
        string code, title;

        switch (ex)
        {
            case ValidationException v:
                (status, code, title) = (400, "VALIDATION_ERROR", "Validation failed");
                detail = "The request body or parameters are invalid.";
                errors = v.Errors
                    .Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                    .ToList();
                break;

            case AppException a:
                (status, code, title) = (a.Status, a.Code, a.Title);
                detail = a.Message;
                break;

            default:
                (status, code, title) = (500, "INTERNAL_ERROR", "Unexpected error");
                break;
        }

        if (status >= 500) log.LogError(ex, "Unhandled exception");

        ctx.Response.StatusCode = status;
        ctx.Response.Headers["X-Correlation-ID"] = correlationId;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = "https://errors.labbank.example/" + code.ToLower().Replace('_', '-')
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlation_id"] = correlationId;
        if (errors is not null) problem.Extensions["errors"] = errors;

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = problem
        });
    }
}