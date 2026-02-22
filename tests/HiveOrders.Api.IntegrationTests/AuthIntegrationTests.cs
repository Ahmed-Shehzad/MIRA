using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HiveOrders.Api.Features.Auth;
using HiveOrders.Api.Shared.Data;
using Xunit;

namespace HiveOrders.Api.IntegrationTests;

[Collection("Integration")]
public class AuthIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AuthIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateClient() => _fixture.Factory.CreateClient();

    private static async Task<AuthResponse> GetTestTokenAsync(HttpClient client, string email, string company = "TestCo")
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/test-token", new TestTokenRequest(email, company));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return auth;
    }

    [Fact]
    public async Task GetTestToken_ValidRequest_ReturnsToken()
    {
        var client = CreateClient();
        var uniqueEmail = $"token-{Guid.NewGuid():N}@hive.local";

        var auth = await GetTestTokenAsync(client, uniqueEmail);

        Assert.NotEmpty(auth.Token);
        Assert.Equal(uniqueEmail, auth.Email);
        Assert.Equal("TestCo", auth.Company);
        Assert.Contains(DbInitializer.GroupUsers, auth.Groups);
    }

    [Fact]
    public async Task GetMe_WithValidToken_ReturnsUser()
    {
        var client = CreateClient();
        var uniqueEmail = $"me-{Guid.NewGuid():N}@hive.local";
        var auth = await GetTestTokenAsync(client, uniqueEmail);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(me);
        Assert.Equal(uniqueEmail, me.Email);
        Assert.Equal("TestCo", me.Company);
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
