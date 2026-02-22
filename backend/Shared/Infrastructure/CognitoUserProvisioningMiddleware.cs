using System.Security.Claims;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Shared.Infrastructure;

/// <summary>
/// Middleware that provisions AppUser from Cognito token and replaces the principal
/// with one that has our user Id and tenant_id. Runs after JWT authentication.
/// </summary>
public sealed class CognitoUserProvisioningMiddleware
{
    private const string CognitoGroupsClaim = "cognito:groups";
    private const string TenantIdClaim = "tenant_id";
    private const string CustomTenantIdClaim = "custom:tenant_id";
    private const string CustomCompanyClaim = "custom:company";
    private const string CognitoUsernameClaim = "cognito:username";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public CognitoUserProvisioningMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context, ICognitoUserService cognitoUserService)
    {
        if (!IsCognitoMode() || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var cognitoSub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(cognitoSub) || !IsCognitoIssuer(context.User))
        {
            await _next(context);
            return;
        }

        var email = context.User.FindFirstValue(ClaimTypes.Email) ?? context.User.FindFirstValue("email");
        var cognitoUsername = context.User.FindFirstValue(CognitoUsernameClaim);
        var cognitoGroups = GetCognitoGroups(context.User);
        var customTenantId = context.User.FindFirstValue(CustomTenantIdClaim);
        var customCompany = context.User.FindFirstValue(CustomCompanyClaim);

        var user = await cognitoUserService.ProvisionOrFindAsync(
            cognitoSub,
            email,
            cognitoUsername,
            cognitoGroups,
            customTenantId,
            customCompany,
            context.RequestAborted);

        if (user == null)
        {
            await _next(context);
            return;
        }

        var claims = context.User.Claims
            .Where(c => c.Type != ClaimTypes.NameIdentifier && c.Type != ClaimTypes.Role && c.Type != TenantIdClaim)
            .ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.Value));
        claims.Add(new Claim(TenantIdClaim, user.TenantId.ToString()));
        foreach (var group in user.Groups)
            claims.Add(new Claim(ClaimTypes.Role, group.Value));

        var identity = new ClaimsIdentity(
            claims,
            context.User.Identity?.AuthenticationType ?? "Bearer",
            ClaimTypes.Name,
            ClaimTypes.Role);
        context.User = new ClaimsPrincipal(identity);
        await _next(context);
    }

    private bool IsCognitoMode() =>
        !string.IsNullOrWhiteSpace(_configuration["AWS:Cognito:UserPoolId"])
        && !string.IsNullOrWhiteSpace(_configuration["AWS:Cognito:Region"] ?? _configuration["AWS:Region"]);

    private static bool IsCognitoIssuer(ClaimsPrincipal principal)
    {
        var issuer = principal.FindFirstValue("iss");
        return issuer != null && issuer.Contains("cognito-idp.", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetCognitoGroups(ClaimsPrincipal principal)
    {
        var groups = principal.FindAll(CognitoGroupsClaim).Select(c => c.Value).ToList();
        return groups.Count > 0 ? groups : [DbInitializer.GroupUsers];
    }
}
