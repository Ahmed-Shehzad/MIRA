using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;
using HiveOrders.Api.Tests;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HiveOrders.Api.Tests.Features.OrderRounds;

[Collection("Postgres")]
public class OrderRoundHandlerTests
{
    private readonly PostgreSqlFixture _fixture;

    public OrderRoundHandlerTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private static IOrderRoundHandler CreateHandler(ApplicationDbContext db, int tenantId = 1)
    {
        var tenantContext = new Mock<ITenantContext>();
        tenantContext.Setup(t => t.TenantId).Returns((TenantId?)new TenantId(tenantId));
        return new OrderRoundHandler(db, Mock.Of<IPublishEndpoint>(), tenantContext.Object);
    }

    [Fact]
    public async Task CreateAsync_StoresRound_ReturnsResponse()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);

        var request = new CreateOrderRoundRequest(
            "Pizza Place",
            "https://pizza.example.com",
            DateTime.UtcNow.AddHours(2));

        var result = await handler.CreateAsync(request, (UserId)TestDbContextFactory.TestUserId);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Pizza Place", result.RestaurantName);
        Assert.Equal("Open", result.Status);
        Assert.Equal(0, result.ItemCount);

        var stored = await db.OrderRounds.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal(TestDbContextFactory.TestUserId, stored.CreatedByUserId.Value);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingRound_ReturnsDetail()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);

        var createRequest = new CreateOrderRoundRequest(
            "Sushi Bar",
            null,
            DateTime.UtcNow.AddHours(1));
        var created = await handler.CreateAsync(createRequest, (UserId)TestDbContextFactory.TestUserId);

        var result = await handler.GetByIdAsync((OrderRoundId)created.Id, (UserId)TestDbContextFactory.TestUserId);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Sushi Bar", result.RestaurantName);
        Assert.Equal("creator@hive.local", result.CreatedByUserEmail);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);

        var result = await handler.GetByIdAsync((OrderRoundId)99999, (UserId)TestDbContextFactory.TestUserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task AddItemAsync_OpenRound_AddsItem()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddHours(3)),
            (UserId)TestDbContextFactory.TestUserId);

        var itemRequest = new CreateOrderItemRequest("Burger", 12.50m, "No onions");
        var item = await handler.AddItemAsync((OrderRoundId)created.Id, itemRequest, (UserId)TestDbContextFactory.OtherUserId);

        Assert.NotNull(item);
        Assert.Equal(created.Id, item.OrderRoundId);
        Assert.Equal("Burger", item.Description);
        Assert.Equal(12.50m, item.Price);
        Assert.Equal("No onions", item.Notes);
        Assert.Equal("other@hive.local", item.UserEmail);
    }

    [Fact]
    public async Task AddItemAsync_ClosedRound_ReturnsNull()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddHours(1)),
            (UserId)TestDbContextFactory.TestUserId);
        await handler.UpdateAsync((OrderRoundId)created.Id, new UpdateOrderRoundRequest(null, null, null, Close: true), (UserId)TestDbContextFactory.TestUserId);

        var item = await handler.AddItemAsync((OrderRoundId)created.Id, new CreateOrderItemRequest("Coffee", 3m, null), (UserId)TestDbContextFactory.OtherUserId);

        Assert.Null(item);
    }

    [Fact]
    public async Task AddItemAsync_PastDeadline_ReturnsNull()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddMinutes(-5)),
            (UserId)TestDbContextFactory.TestUserId);

        var item = await handler.AddItemAsync((OrderRoundId)created.Id, new CreateOrderItemRequest("Coffee", 3m, null), (UserId)TestDbContextFactory.OtherUserId);

        Assert.Null(item);
    }

    [Fact]
    public async Task UpdateAsync_AsCreator_UpdatesRound()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Old Name", "https://old.com", DateTime.UtcNow.AddHours(2)),
            (UserId)TestDbContextFactory.TestUserId);

        var updateRequest = new UpdateOrderRoundRequest("New Name", "https://new.com", DateTime.UtcNow.AddHours(4), null);
        var result = await handler.UpdateAsync((OrderRoundId)created.Id, updateRequest, (UserId)TestDbContextFactory.TestUserId);

        Assert.NotNull(result);
        Assert.Equal("New Name", result.RestaurantName);
        Assert.Equal("https://new.com", result.RestaurantUrl);
    }

    [Fact]
    public async Task UpdateAsync_AsNonCreator_ReturnsNull()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddHours(2)),
            (UserId)TestDbContextFactory.TestUserId);

        var result = await handler.UpdateAsync((OrderRoundId)created.Id, new UpdateOrderRoundRequest("Hacked", null, null, null), (UserId)TestDbContextFactory.OtherUserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveItemAsync_OwnItem_Removes()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddHours(2)),
            (UserId)TestDbContextFactory.TestUserId);
        var item = await handler.AddItemAsync((OrderRoundId)created.Id, new CreateOrderItemRequest("Tea", 2m, null), (UserId)TestDbContextFactory.OtherUserId);
        Assert.NotNull(item);

        var removed = await handler.RemoveItemAsync((OrderRoundId)created.Id, (OrderItemId)item.Id, (UserId)TestDbContextFactory.OtherUserId);

        Assert.True(removed);
        var stillThere = await db.OrderItems.FindAsync([item.Id]);
        Assert.Null(stillThere);
    }

    [Fact]
    public async Task RemoveItemAsync_OthersItem_ReturnsFalse()
    {
        await using var db = await TestDbContextFactory.CreateWithSeedAsync(_fixture.ConnectionString);
        var handler = CreateHandler(db);
        var created = await handler.CreateAsync(
            new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddHours(2)),
            (UserId)TestDbContextFactory.TestUserId);
        var item = await handler.AddItemAsync((OrderRoundId)created.Id, new CreateOrderItemRequest("Tea", 2m, null), (UserId)TestDbContextFactory.OtherUserId);
        Assert.NotNull(item);

        var removed = await handler.RemoveItemAsync((OrderRoundId)created.Id, (OrderItemId)item.Id, (UserId)TestDbContextFactory.TestUserId);

        Assert.False(removed);
    }
}
