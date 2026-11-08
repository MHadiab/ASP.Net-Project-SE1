using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

//import .env file
try { DotNetEnv.Env.TraversePath().Load(); }
catch (FileNotFoundException) { }

var builder = WebApplication.CreateBuilder(args);

//overrides the default logs and displays the log as JSON in the console.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings__Default is not set");
//postgres
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(connectionString));

//JSON
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = ctx =>
        {
            var problem = new ProblemDetails
            {
                Type = "https://errors.labbank.example/error-validation",
                Title = "Validation failed",
                Status = 400,
                Detail = "The request body or parameters are invalid."
            };
            problem.Extensions["code"] = "ERROR_VALIDATION";
            problem.Extensions["correlation_id"] = ctx.HttpContext.TraceIdentifier;
            problem.Extensions["errors"] = ctx.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .Select(kv => new { field = kv.Key, message = kv.Value!.Errors[0].ErrorMessage });

            return new ObjectResult(problem)
            {
                StatusCode = 400,
                ContentTypes = { "application/problem+json" }
            };
        };
    });

builder.Services.AddScoped<IHoldService, HoldService>();
builder.Services.AddScoped<ILedgerAccountService, LedgerAccountService>();
builder.Services.AddScoped<IPostingService, PostingService>();
builder.Services.AddScoped<IJournalEntriesService, JournalEntriesService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

//swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//CORS: allow the React (Vite) frontend
var allowedOrigins = new List<string>
{
    "http://localhost:5173",
    "http://127.0.0.1:5173"
};

if (builder.Environment.IsDevelopment())
{
    allowedOrigins.Add("http://localhost:8088");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(allowedOrigins.ToArray())
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("X-Correlation-ID"));
});


var app = builder.Build();

if (app.Configuration.GetValue<bool>("SeedDemo"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Demo seeding is only available in Development.");
    }

    await using var scope = app.Services.CreateAsyncScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var postingService =
        scope.ServiceProvider.GetRequiredService<IPostingService>();

    await LedgerDemoSeed.RunAsync(db, postingService);
}

// System accounts (CLEARING, CASH_ISSUANCE) are created at startup if missing.
// Set SeedSystemAccounts=false to skip (for example in tests that create them themselves).
if (app.Configuration.GetValue("SeedSystemAccounts", true))
{
    try
    {
        await using var seedScope = app.Services.CreateAsyncScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SystemAccountSeed.EnsureAsync(seedDb);
    }
    catch (Exception ex)
    {
        // For example the database is down or migrations are not applied yet.
        app.Logger.LogWarning(ex, "System accounts could not be seeded at startup.");
    }
}

app.UseCors("Frontend");
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();

public partial class Program { }