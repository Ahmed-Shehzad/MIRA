using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.Infrastructure;
using MassTransit;

namespace HiveOrders.Api.Features.OrderRounds;

public interface IOrderRoundHandler
{
    Task<OrderRoundDetailResponse?> GetByIdAsync(int id, string? userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderRoundResponse>> GetMyOrderRoundsAsync(string userId, CancellationToken cancellationToken = default);
    Task<OrderRoundResponse> CreateAsync(CreateOrderRoundRequest request, string userId, CancellationToken cancellationToken = default);
    Task<OrderRoundResponse?> UpdateAsync(int id, UpdateOrderRoundRequest request, string userId, CancellationToken cancellationToken = default);
    Task<OrderItemResponse?> AddItemAsync(int orderRoundId, CreateOrderItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<OrderItemResponse?> UpdateItemAsync(int orderRoundId, int itemId, UpdateOrderItemRequest request, string userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(int orderRoundId, int itemId, string userId, CancellationToken cancellationToken = default);
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

    public async Task<OrderRoundDetailResponse?> GetByIdAsync(int id, string? userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;

        var round = await _db.OrderRounds
            .Where(o => o.TenantId == tenantId.Value)
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.User)
            .Include(o => o.CreatedByUser)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        return round == null ? null : MapToDetailResponse(round);
    }

    public async Task<IReadOnlyList<OrderRoundResponse>> GetMyOrderRoundsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return [];

        var rounds = await _db.OrderRounds
            .Where(o => o.TenantId == tenantId.Value && (o.CreatedByUserId == userId || o.OrderItems.Any(i => i.UserId == userId)))
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.Deadline)
            .ToListAsync(cancellationToken);

        return rounds.Select(r => new OrderRoundResponse(
            r.Id,
            r.RestaurantName,
            r.RestaurantUrl,
            r.CreatedByUserId,
            r.Deadline,
            r.Status.ToString(),
            r.OrderItems.Count)).ToList();
    }

    public async Task<OrderRoundResponse> CreateAsync(CreateOrderRoundRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");
        var round = new OrderRound
        {
            TenantId = tenantId,
            RestaurantName = request.RestaurantName,
            RestaurantUrl = request.RestaurantUrl,
            CreatedByUserId = userId,
            Deadline = request.Deadline,
            Status = OrderRoundStatus.Open
        };

        _db.OrderRounds.Add(round);
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new OrderRoundCreatedEvent(
            round.Id, round.RestaurantName, round.CreatedByUserId, round.Deadline), cancellationToken);

        return new OrderRoundResponse(
            round.Id,
            round.RestaurantName,
            round.RestaurantUrl,
            round.CreatedByUserId,
            round.Deadline,
            round.Status.ToString(),
            0);
    }

    public async Task<OrderRoundResponse?> UpdateAsync(int id, UpdateOrderRoundRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var round = await _db.OrderRounds
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id && o.TenantId == tenantId.Value && o.CreatedByUserId == userId, cancellationToken);

        if (round == null || round.Status == OrderRoundStatus.Closed)
            return null;

        if (request.RestaurantName != null) round.RestaurantName = request.RestaurantName;
        if (request.RestaurantUrl != null) round.RestaurantUrl = request.RestaurantUrl;
        if (request.Deadline.HasValue) round.Deadline = request.Deadline.Value;
        if (request.Close == true)
        {
            round.Status = OrderRoundStatus.Closed;
            await _publishEndpoint.Publish(new OrderRoundClosedEvent(round.Id, round.CreatedByUserId), cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new OrderRoundResponse(
            round.Id,
            round.RestaurantName,
            round.RestaurantUrl,
            round.CreatedByUserId,
            round.Deadline,
            round.Status.ToString(),
            round.OrderItems.Count);
    }

    public async Task<OrderItemResponse?> AddItemAsync(int orderRoundId, CreateOrderItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var round = await _db.OrderRounds.FirstOrDefaultAsync(o => o.Id == orderRoundId && o.TenantId == tenantId.Value, cancellationToken);
        if (round == null || round.Status == OrderRoundStatus.Closed || round.Deadline < DateTime.UtcNow)
            return null;

        var item = new OrderItem
        {
            OrderRoundId = orderRoundId,
            UserId = userId,
            Description = request.Description,
            Price = request.Price,
            Notes = request.Notes
        };

        _db.OrderItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new OrderItemAddedEvent(
            orderRoundId, item.Id, userId, item.Description, item.Price), cancellationToken);

        var user = await _db.Users.FindAsync([userId], cancellationToken);
        return new OrderItemResponse(
            item.Id,
            item.OrderRoundId,
            item.UserId,
            user?.Email ?? "",
            item.Description,
            item.Price,
            item.Notes);
    }

    public async Task<OrderItemResponse?> UpdateItemAsync(int orderRoundId, int itemId, UpdateOrderItemRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var item = await _db.OrderItems
            .Include(i => i.User)
            .Include(i => i.OrderRound)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.OrderRoundId == orderRoundId && i.UserId == userId && i.OrderRound.TenantId == tenantId.Value, cancellationToken);

        if (item == null) return null;

        var round = await _db.OrderRounds.FindAsync([orderRoundId], cancellationToken);
        if (round == null || round.Status == OrderRoundStatus.Closed || round.Deadline < DateTime.UtcNow)
            return null;

        if (request.Description != null) item.Description = request.Description;
        if (request.Price.HasValue) item.Price = request.Price.Value;
        if (request.Notes != null) item.Notes = request.Notes;

        await _db.SaveChangesAsync(cancellationToken);

        return new OrderItemResponse(
            item.Id,
            item.OrderRoundId,
            item.UserId,
            item.User.Email ?? "",
            item.Description,
            item.Price,
            item.Notes);
    }

    public async Task<bool> RemoveItemAsync(int orderRoundId, int itemId, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return false;
        var item = await _db.OrderItems
            .Include(i => i.OrderRound)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.OrderRoundId == orderRoundId && i.UserId == userId && i.OrderRound.TenantId == tenantId.Value, cancellationToken);

        if (item == null) return false;

        var round = await _db.OrderRounds.FindAsync([orderRoundId], cancellationToken);
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
            i.UserId,
            i.User.Email ?? "",
            i.Description,
            i.Price,
            i.Notes)).ToList();

        return new OrderRoundDetailResponse(
            round.Id,
            round.RestaurantName,
            round.RestaurantUrl,
            round.CreatedByUserId,
            round.CreatedByUser.Email ?? "",
            round.Deadline,
            round.Status.ToString(),
            items);
    }
}
