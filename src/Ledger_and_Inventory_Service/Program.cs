using System.Text.Json;
using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
using Ledger_and_Inventory_Service.Services.Interfaces;
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

builder.Services.AddScoped<IPostingService, PostingService>();

builder.Services.AddScoped<IJournalEntriesService, JournalEntriesService>();

// builder.Services.AddScoped<IHoldService, HoldService>();


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

app.UseCors("Frontend");
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();

public partial class Program { }
