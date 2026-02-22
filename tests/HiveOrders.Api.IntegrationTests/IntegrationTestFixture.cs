using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace HiveOrders.Api.IntegrationTests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("hive_orders_test")
        .WithUsername("hive")
        .WithPassword("hive_test")
        .Build();

    private HiveOrdersWebApplicationFactory? _factory;

    public HiveOrdersWebApplicationFactory Factory => _factory
        ?? throw new InvalidOperationException("Fixture not initialized. Ensure IAsyncLifetime.InitializeAsync was called.");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();
        await WaitForDatabaseReadyAsync(connectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("Testing__SkipRateLimiting", "true");
        Environment.SetEnvironmentVariable("Testing__UseLocalJwt", "true");
        Environment.SetEnvironmentVariable("Jwt__Key", HiveOrdersWebApplicationFactory.TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "HiveOrders");
        Environment.SetEnvironmentVariable("Jwt__Audience", "HiveOrders");
        Environment.SetEnvironmentVariable("AWS__Region", "us-east-1");
        _factory = new HiveOrdersWebApplicationFactory();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private static async Task WaitForDatabaseReadyAsync(string connectionString)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();
                return;
            }
            catch (NpgsqlException ex)
            {
                lastException = ex;
                if (attempt < MaxRetries)
                    await Task.Delay(RetryDelay);
            }
        }
        throw new InvalidOperationException(
            $"PostgreSQL did not accept connections after {MaxRetries} attempts. Connection refused or timeout.", lastException);
    }
}
