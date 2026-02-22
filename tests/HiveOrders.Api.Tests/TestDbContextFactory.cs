using Microsoft.EntityFrameworkCore;
using Npgsql;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Tests;

public static class TestDbContextFactory
{
    public const string TestUserId = "test-user-111";
    public const string OtherUserId = "test-user-222";

    public static async Task<ApplicationDbContext> CreateWithSeedAsync(string baseConnectionString)
    {
        var dbName = "hive_test_" + Guid.NewGuid().ToString("N")[..12];

        await using (var conn = new NpgsqlConnection(baseConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                CREATE DATABASE "{dbName}"
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = dbName
        };
        var connectionString = builder.ConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();

        var tenant = await db.Tenants.FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var user1 = new AppUser
        {
            Id = (UserId)TestUserId,
            CognitoUsername = "creator@hive.local",
            Email = (Email)"creator@hive.local",
            Company = "TestCo",
            TenantId = tenant.Id,
            Groups = [(UserGroup)DbInitializer.GroupUsers],
            CreatedAt = now,
            UpdatedAt = now
        };

        var user2 = new AppUser
        {
            Id = (UserId)OtherUserId,
            CognitoUsername = "other@hive.local",
            Email = (Email)"other@hive.local",
            Company = "TestCo",
            TenantId = tenant.Id,
            Groups = [(UserGroup)DbInitializer.GroupUsers],
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Users.Add(user1);
        db.Users.Add(user2);
        await db.SaveChangesAsync();

        return db;
    }
}
