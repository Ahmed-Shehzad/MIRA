using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Notifications;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;

    public NotificationsController(INotificationService notificationService, ITenantContext tenantContext)
    {
        _notificationService = notificationService;
        _tenantContext = tenantContext;
    }

    /// <summary>Get VAPID public key for push subscription. Returns null if push is not configured.</summary>
    [HttpGet("push/vapid-public-key")]
    [ProducesResponseType(typeof(VapidPublicKeyResponse), StatusCodes.Status200OK)]
    public ActionResult<VapidPublicKeyResponse> GetVapidPublicKey()
    {
        var key = _notificationService.GetVapidPublicKey();
        return Ok(new VapidPublicKeyResponse(string.IsNullOrWhiteSpace(key) ? null : key));
    }

    /// <summary>Get unread notifications for the current user. Poll for in-app alerts.</summary>
    [HttpGet("unread")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetUnread(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = _tenantContext.TenantId;
        if (userId == null || tenantId == null) return Unauthorized();

        var notifications = await _notificationService.GetUnreadAsync(new UserId(userId), tenantId.Value, cancellationToken);
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
        if (userId == null || tenantId == null) return Unauthorized();

        var found = await _notificationService.MarkReadAsync(id, new UserId(userId), tenantId.Value, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Subscribe to push notifications. Requires VAPID keys in config.</summary>
    [HttpPost("push/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubscribePush([FromBody] PushSubscribeRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tenantId = _tenantContext.TenantId;
        if (userId == null || tenantId == null) return Unauthorized();

        await _notificationService.SubscribePushAsync(request, new UserId(userId), tenantId.Value, cancellationToken);
        return NoContent();
    }
}

public record VapidPublicKeyResponse(string? VapidPublicKey);
public record PushSubscribeRequest(string Endpoint, PushSubscribeKeys? Keys);
public record PushSubscribeKeys(string? P256dh, string? Auth);

public record NotificationResponse(int Id, string Type, string Title, string Body, DateTime CreatedAt);
