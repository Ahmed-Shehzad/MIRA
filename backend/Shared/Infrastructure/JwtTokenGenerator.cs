using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Generate(AppUser user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key required for test token.");
        var issuer = _configuration["Jwt:Issuer"] ?? "HiveOrders.Test";
        var audience = _configuration["Jwt:Audience"] ?? "HiveOrders.Test";
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value),
            new("tenant_id", user.TenantId.ToString())
        };
        foreach (var g in user.Groups)
            claims.Add(new Claim(ClaimTypes.Role, g.Value));
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
