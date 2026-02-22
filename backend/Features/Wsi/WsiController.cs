using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Wsi;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/wsi")]
[Authorize]
public class WsiController : ControllerBase
{
    private readonly IWsiHandler _handler;

    public WsiController(IWsiHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Get presigned upload URL for WSI. Per high_level_platform.md Phase 1 MVP.</summary>
    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(WsiPresignedUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WsiPresignedUrlResponse>> GetUploadUrl(
        [FromBody] WsiPresignedUrlRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var response = await _handler.GetPresignedUploadUrlAsync(request, new UserId(userId), cancellationToken);
        return response == null ? StatusCode(503, new { message = "S3 storage not configured" }) : Ok(response);
    }

    /// <summary>Register WSI upload metadata after client uploads to S3.</summary>
    [HttpPost("uploads")]
    [ProducesResponseType(typeof(WsiUploadResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<WsiUploadResponse>> CreateUpload(
        [FromBody] CreateWsiUploadRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var response = await _handler.CreateUploadAsync(request, new UserId(userId), cancellationToken);
        return CreatedAtAction(nameof(GetUpload), new { id = response!.Id }, response);
    }

    /// <summary>Get WSI upload by ID.</summary>
    [HttpGet("uploads/{id:guid}")]
    [ProducesResponseType(typeof(WsiUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WsiUploadResponse>> GetUpload(Guid id, CancellationToken cancellationToken)
    {
        var upload = await _handler.GetUploadAsync(new WsiUploadId(id), cancellationToken);
        return upload == null ? NotFound() : Ok(upload);
    }

    /// <summary>List WSI uploads for current user.</summary>
    [HttpGet("uploads")]
    [ProducesResponseType(typeof(IReadOnlyList<WsiUploadResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WsiUploadResponse>>> GetUploads(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var uploads = await _handler.GetUploadsAsync(new UserId(userId), cancellationToken);
        return Ok(uploads);
    }

    /// <summary>Trigger analysis on a WSI. Per high_level_platform.md Phase 1 MVP – manual analysis trigger.</summary>
    [HttpPost("uploads/{id:guid}/analyze")]
    [ProducesResponseType(typeof(WsiJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WsiJobResponse>> TriggerAnalysis(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var job = await _handler.TriggerAnalysisAsync(new WsiUploadId(id), new UserId(userId), cancellationToken);
        return job == null ? NotFound() : AcceptedAtAction(nameof(GetJob), new { id = job.Id }, job);
    }

    /// <summary>Get WSI job status.</summary>
    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(typeof(WsiJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WsiJobResponse>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await _handler.GetJobAsync(new WsiJobId(id), cancellationToken);
        return job == null ? NotFound() : Ok(job);
    }
}

public record WsiPresignedUrlRequest(string FileName, string? ContentType);
public record WsiPresignedUrlResponse(string Url, string Key);
