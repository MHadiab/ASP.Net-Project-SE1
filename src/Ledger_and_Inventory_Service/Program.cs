using System.Text.Json;
using FluentValidation;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Ledger_and_Inventory_Service.Validators;
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
builder.Services.AddScoped<IPostingService, PostingService>();
builder.Services.AddScoped<IJournalEntriesService, JournalEntriesService>();


builder.Services.AddValidatorsFromAssemblyContaining<CreateHoldValidator>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

//swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//CORS: allow the React (Vite) frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://localhost:5037", "http://127.0.0.1:5037")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("X-Correlation-ID"));
});

var app = builder.Build();
app.UseCors("Frontend");
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();

public partial class Program { }