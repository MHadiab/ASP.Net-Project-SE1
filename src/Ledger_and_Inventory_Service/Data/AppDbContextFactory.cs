using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ledger_and_Inventory_Service.Data;

public class AppDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        Env.TraversePath().Load();

        var useTestDatabase = args.Contains(
            "--test",
            StringComparer.OrdinalIgnoreCase
        );

        var connectionString = useTestDatabase
            ? Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
            : Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string was not found in .env"
            );
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}