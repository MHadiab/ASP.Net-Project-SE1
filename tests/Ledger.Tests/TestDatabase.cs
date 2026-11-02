using DotNetEnv;
using Ledger_and_Inventory_Service.Data;
using Microsoft.EntityFrameworkCore;

namespace Ledger.Tests;

public static class TestDatabase
{
    public static AppDbContext CreateContext()
    {
        Env.TraversePath().Load();

        var connectionString =
            Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "TEST_CONNECTION_STRING is not set");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}