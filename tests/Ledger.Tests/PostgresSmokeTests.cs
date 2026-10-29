using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public class PostgresSmokeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public Task InitializeAsync() => _db.StartAsync();
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Can_connect_to_a_real_postgres()
    {
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);

        var result = await cmd.ExecuteScalarAsync();

        Assert.Equal(1, Convert.ToInt32(result));
    }
}