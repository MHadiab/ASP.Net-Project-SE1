using Ledger_and_Inventory_Service.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Ledger.Tests.Infrastructure;

public class PostgresTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithImage("postgres:17-alpine")
            .Build();

    public CustomWebApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Factory = new CustomWebApplicationFactory(
            _database.GetConnectionString());

        using var scope = Factory.Services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}