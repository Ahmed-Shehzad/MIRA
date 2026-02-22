using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HiveOrders.Api.Features.Auth;
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

    [Fact]
    public async Task Register_ValidRequest_ReturnsOk()
    {
        var client = CreateClient();
        var request = new RegisterRequest("integration-test@hive.local", "Password1!", "TestCo");

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Registration successful. Please check your email to confirm your account.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var client = CreateClient();
        var request = new RegisterRequest("dup@hive.local", "Password1!", "TestCo");
        await client.PostAsJsonAsync("/api/v1/auth/register", request);

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ConfirmedUser_ReturnsToken()
    {
        var client = CreateClient();
        var uniqueEmail = $"login-{Guid.NewGuid():N}@hive.local";
        var registerRequest = new RegisterRequest(uniqueEmail, "Password1!", "TestCo");
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new LoginRequest(uniqueEmail, "Password1!");
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.NotEmpty(auth.Token);
        Assert.Equal(uniqueEmail, auth.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var client = CreateClient();
        var registerRequest = new RegisterRequest("badpwd@hive.local", "Password1!", "TestCo");
        await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest("badpwd@hive.local", "WrongPassword1!");
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_WithValidToken_ReturnsUser()
    {
        var client = CreateClient();
        var uniqueEmail = $"me-{Guid.NewGuid():N}@hive.local";
        var registerRequest = new RegisterRequest(uniqueEmail, "Password1!", "TestCo");
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();
        var loginRequest = new LoginRequest(uniqueEmail, "Password1!");
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(me);
        Assert.Equal(uniqueEmail, me.Email);
    }

    [Fact]
    public async Task GetMe_WithoutToken_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
