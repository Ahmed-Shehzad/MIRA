using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;

namespace HiveOrders.Api.Features.Notifications;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;

    public NotificationsController(ApplicationDbContext db, ITenantContext tenantContext, IConfiguration configuration)
    {
        _db = db;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    /// <summary>Get VAPID public key for push subscription. Returns null if push is not configured.</summary>
    [HttpGet("push/vapid-public-key")]
    [ProducesResponseType(typeof(VapidPublicKeyResponse), StatusCodes.Status200OK)]
    public ActionResult<VapidPublicKeyResponse> GetVapidPublicKey()
    {
        var key = _configuration["Push:VapidPublicKey"];
        if (string.IsNullOrWhiteSpace(key))
            return Ok(new VapidPublicKeyResponse(null));
        return Ok(new VapidPublicKeyResponse(key));
    }

    /// <summary>Get unread notifications for the current user. Poll for in-app alerts.</summary>
    [HttpGet("unread")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetUnread(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = _tenantContext.TenantId;
        if (userId == null || tenantId == null)
            return Unauthorized();

        var notifications = await _db.Set<Notification>()
            .Where(n => n.UserId == userId && n.TenantId == tenantId.Value && n.ReadAt == null)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.Title, n.Body ?? "", n.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(notifications);
    }

    /// <summary>Mark a notification as read.</summary>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = _tenantContext.TenantId;
        if (userId == null || tenantId == null)
            return Unauthorized();

        var notification = await _db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && n.TenantId == tenantId.Value, cancellationToken);

        if (notification == null)
            return NotFound();

        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Subscribe to push notifications. Requires VAPID keys in config.</summary>
    [HttpPost("push/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubscribePush([FromBody] PushSubscribeRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = _tenantContext.TenantId;
        if (userId == null || tenantId == null)
            return Unauthorized();

        var existing = await _db.Set<PushSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value && s.UserId == userId && s.Endpoint == request.Endpoint, cancellationToken);
        if (existing != null)
            return NoContent();

        _db.Set<PushSubscription>().Add(new PushSubscription
        {
            TenantId = tenantId.Value,
            UserId = userId,
            Endpoint = request.Endpoint,
            P256dh = request.Keys?.P256dh,
            Auth = request.Keys?.Auth
        });
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public record VapidPublicKeyResponse(string? VapidPublicKey);
public record PushSubscribeRequest(string Endpoint, PushSubscribeKeys? Keys);
public record PushSubscribeKeys(string? P256dh, string? Auth);

public record NotificationResponse(int Id, string Type, string Title, string Body, DateTime CreatedAt);
