using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Errors;
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


builder.Services.AddControllers()
    //JSON snake-case converter
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy =
        JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

//swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();