using System.IdentityModel.Tokens.Jwt;
using HiveOrders.Api.Features.Auth;
using HiveOrders.Api.Shared.Identity;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HiveOrders.Api.Tests.Features.Auth;

public class JwtTokenServiceTests
{
    private static IConfiguration CreateConfig(string key = "test-key-must-be-at-least-32-chars-for-hs256!")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = key,
                ["Jwt:Issuer"] = "HiveOrders.Test",
                ["Jwt:Audience"] = "HiveOrders.Test",
                ["Jwt:ExpirationMinutes"] = "60"
            })
            .Build();
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwt()
    {
        var config = CreateConfig();
        var service = new JwtTokenService(config);
        var user = new ApplicationUser
        {
            Id = "user-123",
            UserName = "test@hive.local",
            Email = "test@hive.local",
            Company = "TestCo"
        };

        var token = service.GenerateToken(user, ["User"]);

        Assert.NotNull(token);
        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal("HiveOrders.Test", jwt.Issuer);
        Assert.Equal("HiveOrders.Test", jwt.Audiences.First());
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && c.Value == "user-123");
        Assert.Contains(jwt.Claims, c => c.Type == "Company" && c.Value == "TestCo");
        Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public void GenerateToken_MissingKey_Throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var service = new JwtTokenService(config);
        var user = new ApplicationUser { Id = "x", UserName = "x", Email = "x@x.com", Company = "X" };

        Assert.Throws<InvalidOperationException>(() => service.GenerateToken(user, []));
    }
}
