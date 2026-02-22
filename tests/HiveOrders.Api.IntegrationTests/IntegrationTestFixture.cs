using Testcontainers.PostgreSql;
using Xunit;

namespace HiveOrders.Api.IntegrationTests;

public sealed class IntegrationTestFixture : IAsyncLifetime
{
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
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("Testing__SkipRateLimiting", "true");
        Environment.SetEnvironmentVariable("Testing__UseLocalJwt", "true");
        Environment.SetEnvironmentVariable("Jwt__Key", HiveOrdersWebApplicationFactory.TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "HiveOrders");
        Environment.SetEnvironmentVariable("Jwt__Audience", "HiveOrders");
        _factory = new HiveOrdersWebApplicationFactory();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
