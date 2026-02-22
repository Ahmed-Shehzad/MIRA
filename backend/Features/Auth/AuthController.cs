using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HiveOrders.Api.Features.Auth;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    /// <summary>Get test token (Testing:UseLocalJwt only). Provisions user and returns JWT.</summary>
    [HttpPost("test-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResponse>> GetTestToken(
        [FromBody] TestTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(_configuration["Testing:UseLocalJwt"], "true", StringComparison.OrdinalIgnoreCase))
            return NotFound();

        var response = await _authService.GetTestTokenAsync(request, cancellationToken);
        return response == null ? NotFound() : Ok(response);
    }

    /// <summary>Get current authenticated user. Requires Bearer token (Cognito id_token).</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var bearerToken = Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "");
        var response = await _authService.GetCurrentUserAsync(userId, bearerToken, cancellationToken);
        return response == null ? Unauthorized() : Ok(response);
    }
}
