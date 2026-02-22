using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.Infrastructure;

namespace HiveOrders.Api.Features.Auth;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _emailService = emailService;
        _configuration = configuration;
        _db = db;
    }

    private bool SkipEmailConfirmation =>
        string.Equals(_configuration["Testing:SkipEmailConfirmation"], "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Register a new user with email and password.</summary>
    /// <param name="request">Email, password, and company name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success message; user must confirm email before logging in.</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return BadRequest(new { message = "Email already registered." });

        var defaultTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == DbInitializer.DefaultTenantSlug, cancellationToken)
            ?? throw new InvalidOperationException("Default tenant not configured.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            Company = request.Company,
            EmailConfirmed = false,
            TenantId = defaultTenant.Id
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, DbInitializer.RoleUser);

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackUrl = Url.Action(nameof(ConfirmEmail), "Auth", new { userId = user.Id, token }, Request.Scheme)
            ?? $"/api/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await _emailService.SendEmailAsync(
            user.Email!,
            "Confirm your HIVE Food Orders account",
            $"Please confirm your account by visiting: {callbackUrl}",
            cancellationToken);

        return Ok(new { message = "Registration successful. Please check your email to confirm your account." });
    }

    /// <summary>Confirm email address using the token sent via email.</summary>
    [HttpGet("confirm-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return BadRequest(new { message = "Invalid confirmation link." });

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return BadRequest(new { message = "Email confirmation failed.", errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Email confirmed. You can now log in." });
    }

    /// <summary>Authenticate with email and password. Returns JWT token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.EmailConfirmed && !SkipEmailConfirmation)
            return Unauthorized(new { message = "Please confirm your email before logging in." });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = await _userManager.GetRolesAsync(user);
        var jwtToken = _jwtTokenService.GenerateToken(user, roles);
        return Ok(new AuthResponse(jwtToken, user.Email!, user.Company, roles));
    }

    /// <summary>Get current authenticated user. Requires Bearer token.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwtTokenService.GenerateToken(user, roles);
        return Ok(new AuthResponse(token, user.Email!, user.Company, roles));
    }
}
