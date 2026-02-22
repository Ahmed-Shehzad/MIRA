using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HiveOrders.Api.Features.Bot;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/bot")]
[Authorize]
public class BotLinkController : ControllerBase
{
    private readonly IBotLinkService _linkService;

    public BotLinkController(IBotLinkService linkService)
    {
        _linkService = linkService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    /// <summary>Get a one-time code to link your Teams account with the bot.</summary>
    [HttpGet("link-code")]
    [ProducesResponseType(typeof(BotLinkCodeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BotLinkCodeResponse>> GetLinkCode(CancellationToken cancellationToken)
    {
        var code = await _linkService.CreateLinkCodeAsync(UserId, cancellationToken);
        return Ok(new BotLinkCodeResponse(code, "Enter 'link " + code + "' in the Teams bot within 5 minutes."));
    }
}

public record BotLinkCodeResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("code")] string Code,
    [property: System.Text.Json.Serialization.JsonPropertyName("instructions")] string Instructions);
