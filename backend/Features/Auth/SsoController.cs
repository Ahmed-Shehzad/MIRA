using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HiveOrders.Api.Features.Auth;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth/sso")]
[EnableRateLimiting("auth")]
public class SsoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public SsoController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>List available SSO providers (e.g. Google, Microsoft). Empty in development.</summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public IActionResult GetProviders()
    {
        var providers = new List<object>();

        if (!_environment.IsDevelopment())
        {
            if (!string.IsNullOrWhiteSpace(_configuration["Authentication:Google:ClientId"]))
                providers.Add(new { id = "Google", name = "Google" });
            if (!string.IsNullOrWhiteSpace(_configuration["Authentication:Microsoft:ClientId"]))
                providers.Add(new { id = "Microsoft", name = "Microsoft" });
        }

        return Ok(providers);
    }

    /// <summary>Redirect to SSO provider for authentication. Requires provider (Google or Microsoft).</summary>
    [HttpGet("challenge")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public IActionResult Challenge([FromQuery] string provider = "Google")
    {
        if (_environment.IsDevelopment())
            return StatusCode(501, new { message = "SSO is disabled in development." });

        var scheme = provider.ToLowerInvariant() switch
        {
            "google" => Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme,
            "microsoft" => Microsoft.AspNetCore.Authentication.MicrosoftAccount.MicrosoftAccountDefaults.AuthenticationScheme,
            _ => (string?)null
        };

        if (scheme == null)
            return BadRequest(new { message = "Unsupported SSO provider." });

        var clientIdKey = $"Authentication:{provider}:ClientId";
        if (string.IsNullOrWhiteSpace(_configuration[clientIdKey]))
            return StatusCode(501, new { message = $"{provider} SSO is not configured." });

        return Challenge(new AuthenticationProperties(), scheme);
    }
}
