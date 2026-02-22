using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Storage;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/storage")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly IStorageHandler _handler;

    public StorageController(IStorageHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Get presigned upload URL. Production only (S3 configured). Per high_level_platform.md.</summary>
    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(PresignedUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PresignedUrlResponse>> GetUploadUrl(
        [FromBody] PresignedUrlRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var response = await _handler.GetPresignedUploadUrlAsync(request, new UserId(userId), cancellationToken);
        return response == null ? StatusCode(503, new { message = "S3 storage not configured" }) : Ok(response);
    }
}

public record PresignedUrlRequest(string FileName, string? ContentType);
public record PresignedUrlResponse(string Url, string Key);
