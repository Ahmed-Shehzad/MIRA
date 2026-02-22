using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;
using MassTransit;

namespace HiveOrders.Api.Features.OrderRounds;

public interface IOrderRoundHandler
{
    Task<OrderRoundDetailResponse?> GetByIdAsync(OrderRoundId id, UserId? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderRoundResponse>> GetMyOrderRoundsAsync(UserId userId, CancellationToken cancellationToken = default);
    Task<OrderRoundResponse> CreateAsync(CreateOrderRoundRequest request, UserId userId, CancellationToken cancellationToken = default);
    Task<OrderRoundResponse?> UpdateAsync(OrderRoundId id, UpdateOrderRoundRequest request, UserId userId, CancellationToken cancellationToken = default);
    Task<OrderItemResponse?> AddItemAsync(OrderRoundId orderRoundId, CreateOrderItemRequest request, UserId userId, CancellationToken cancellationToken = default);
    Task<OrderItemResponse?> UpdateItemAsync(OrderRoundId orderRoundId, OrderItemId itemId, UpdateOrderItemRequest request, UserId userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(OrderRoundId orderRoundId, OrderItemId itemId, UserId userId, CancellationToken cancellationToken = default);
}

public class OrderRoundHandler : IOrderRoundHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ITenantContext _tenantContext;

    public OrderRoundHandler(ApplicationDbContext db, IPublishEndpoint publishEndpoint, ITenantContext tenantContext)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _tenantContext = tenantContext;
    }

    public async Task<OrderRoundDetailResponse?> GetByIdAsync(OrderRoundId id, UserId? userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;

        var round = await _db.OrderRounds
            .Where(o => o.TenantId == tenantId.Value.Value)
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.User)
            .Include(o => o.CreatedByUser)
            .FirstOrDefaultAsync(o => o.Id == id.Value, cancellationToken);

        return round == null ? null : MapToDetailResponse(round);
    }

    public async Task<IReadOnlyList<OrderRoundResponse>> GetMyOrderRoundsAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return [];

        var rounds = await _db.OrderRounds
            .Where(o => o.TenantId == tenantId.Value.Value && (o.CreatedByUserId == userId || o.OrderItems.Any(i => i.UserId == userId)))
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.Deadline)
            .ToListAsync(cancellationToken);

        return rounds.Select(r => new OrderRoundResponse(
            r.Id,
            r.RestaurantName,
            r.RestaurantUrl,
            r.CreatedByUserId.Value,
            r.Deadline,
            r.Status.Value,
            r.OrderItems.Count)).ToList();
    }

    public async Task<OrderRoundResponse> CreateAsync(CreateOrderRoundRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");
        var round = new OrderRound
        {
            TenantId = tenantId.Value,
            RestaurantName = request.RestaurantName,
            RestaurantUrl = request.RestaurantUrl,
            CreatedByUserId = userId,
            Deadline = request.Deadline,
            Status = OrderRoundStatus.Open
        };

        _db.OrderRounds.Add(round);
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new OrderRoundCreatedEvent(
            round.Id, round.RestaurantName, round.CreatedByUserId.Value, round.Deadline), cancellationToken);

        return new OrderRoundResponse(
            round.Id,
            round.RestaurantName,
            round.RestaurantUrl,
            round.CreatedByUserId.Value,
            round.Deadline,
            round.Status.Value,
            0);
    }

    public async Task<OrderRoundResponse?> UpdateAsync(OrderRoundId id, UpdateOrderRoundRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var round = await _db.OrderRounds
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id.Value && o.TenantId == tenantId.Value.Value && o.CreatedByUserId == userId, cancellationToken);

        if (round == null || round.Status == OrderRoundStatus.Closed)
            return null;

        if (request.RestaurantName != null) round.RestaurantName = request.RestaurantName;
        if (request.RestaurantUrl != null) round.RestaurantUrl = request.RestaurantUrl;
        if (request.Deadline.HasValue) round.Deadline = request.Deadline.Value;
        if (request.Close == true)
        {
            round.Status = OrderRoundStatus.Closed;
            await _publishEndpoint.Publish(new OrderRoundClosedEvent(round.Id, round.CreatedByUserId.Value), cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new OrderRoundResponse(
            round.Id,
            round.RestaurantName,
            round.RestaurantUrl,
            round.CreatedByUserId.Value,
            round.Deadline,
            round.Status.Value,
            round.OrderItems.Count);
    }

    public async Task<OrderItemResponse?> AddItemAsync(OrderRoundId orderRoundId, CreateOrderItemRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var round = await _db.OrderRounds.FirstOrDefaultAsync(o => o.Id == orderRoundId.Value && o.TenantId == tenantId.Value.Value, cancellationToken);
        if (round == null || round.Status == OrderRoundStatus.Closed || round.Deadline < DateTime.UtcNow)
            return null;

        var item = new OrderItem
        {
            OrderRoundId = orderRoundId.Value,
            UserId = userId,
            Description = request.Description,
            Price = (Money)request.Price,
            Notes = request.Notes
        };

        _db.OrderItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new OrderItemAddedEvent(
            orderRoundId.Value, item.Id, userId.Value, item.Description, item.Price.Value), cancellationToken);

        var user = await _db.Users.FindAsync([userId], cancellationToken);
        return new OrderItemResponse(
            item.Id,
            item.OrderRoundId,
            item.UserId.Value,
            user?.Email.Value ?? "",
            item.Description,
            item.Price.Value,
            item.Notes);
    }

    public async Task<OrderItemResponse?> UpdateItemAsync(OrderRoundId orderRoundId, OrderItemId itemId, UpdateOrderItemRequest request, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var item = await _db.OrderItems
            .Include(i => i.User)
            .Include(i => i.OrderRound)
            .FirstOrDefaultAsync(i => i.Id == itemId.Value && i.OrderRoundId == orderRoundId.Value && i.UserId == userId && i.OrderRound.TenantId == tenantId.Value.Value, cancellationToken);

        if (item == null) return null;

        var round = await _db.OrderRounds.FindAsync([orderRoundId.Value], cancellationToken);
        if (round == null || round.Status == OrderRoundStatus.Closed || round.Deadline < DateTime.UtcNow)
            return null;

        if (request.Description != null) item.Description = request.Description;
        if (request.Price.HasValue) item.Price = (Money)request.Price.Value;
        if (request.Notes != null) item.Notes = request.Notes;

        await _db.SaveChangesAsync(cancellationToken);

        return new OrderItemResponse(
            item.Id,
            item.OrderRoundId,
            item.UserId.Value,
            item.User.Email.Value,
            item.Description,
            item.Price.Value,
            item.Notes);
    }

    public async Task<bool> RemoveItemAsync(OrderRoundId orderRoundId, OrderItemId itemId, UserId userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return false;
        var item = await _db.OrderItems
            .Include(i => i.OrderRound)
            .FirstOrDefaultAsync(i => i.Id == itemId.Value && i.OrderRoundId == orderRoundId.Value && i.UserId == userId && i.OrderRound.TenantId == tenantId.Value.Value, cancellationToken);

        if (item == null) return false;

        var round = await _db.OrderRounds.FindAsync([orderRoundId.Value], cancellationToken);
        if (round == null || round.Status == OrderRoundStatus.Closed || round.Deadline < DateTime.UtcNow)
            return false;

        _db.OrderItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static OrderRoundDetailResponse MapToDetailResponse(OrderRound round)
    {
        var items = round.OrderItems.Select(i => new OrderItemResponse(
            i.Id,
            i.OrderRoundId,
            i.UserId.Value,
            i.User.Email.Value,
            i.Description,
            i.Price.Value,
            i.Notes)).ToList();

        return new OrderRoundDetailResponse(
            round.Id,
            round.RestaurantName,
            round.RestaurantUrl,
            round.CreatedByUserId.Value,
            round.CreatedByUser.Email.Value,
            round.Deadline,
            round.Status.Value,
            items);
    }
}
