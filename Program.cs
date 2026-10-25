using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
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

builder.Services.AddScoped<IBalanceService, BalanceService>();


builder.Services.AddControllers()
    //JSON snake-case converter
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy =
        JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

//swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//CORS: allow the React (Vite) frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
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