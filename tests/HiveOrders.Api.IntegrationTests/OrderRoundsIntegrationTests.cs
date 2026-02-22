using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HiveOrders.Api.Features.Auth;
using HiveOrders.Api.Features.OrderRounds;
using Xunit;

namespace HiveOrders.Api.IntegrationTests;

[Collection("Integration")]
public class OrderRoundsIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public OrderRoundsIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private HttpClient CreateClient() => _fixture.Factory.CreateClient();

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var uniqueEmail = $"rounds-{Guid.NewGuid():N}@hive.local";
        var registerRequest = new RegisterRequest(uniqueEmail, "Password1!", "TestCo");
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new LoginRequest(uniqueEmail, "Password1!");
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    [Fact]
    public async Task CreateOrderRound_Authenticated_ReturnsCreated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var request = new CreateOrderRoundRequest("Pizza Place", "https://pizza.example.com", DateTime.UtcNow.AddHours(2));
        var response = await client.PostAsJsonAsync("/api/v1/orderrounds", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var round = await response.Content.ReadFromJsonAsync<OrderRoundResponse>();
        Assert.NotNull(round);
        Assert.True(round.Id > 0);
        Assert.Equal("Pizza Place", round.RestaurantName);
    }


    [Fact]
    public async Task GetOrderRounds_Authenticated_ReturnsList()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/v1/orderrounds");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rounds = await response.Content.ReadFromJsonAsync<OrderRoundResponse[]>();
        Assert.NotNull(rounds);
    }

    [Fact]
    public async Task GetById_ExistingRound_ReturnsDetail()
    {
        var client = await CreateAuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/v1/orderrounds", new CreateOrderRoundRequest("Sushi Bar", null, DateTime.UtcNow.AddHours(1)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<OrderRoundResponse>();
        Assert.NotNull(created);

        var response = await client.GetAsync($"/api/v1/orderrounds/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<OrderRoundDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal("Sushi Bar", detail.RestaurantName);
    }

    [Fact]
    public async Task AddItem_OpenRound_ReturnsCreated()
    {
        var client = await CreateAuthenticatedClientAsync();
        var createResponse = await client.PostAsJsonAsync("/api/v1/orderrounds", new CreateOrderRoundRequest("Cafe", null, DateTime.UtcNow.AddHours(3)));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<OrderRoundResponse>();
        Assert.NotNull(created);

        var itemRequest = new CreateOrderItemRequest("Coffee", 3.50m, "No sugar");
        var response = await client.PostAsJsonAsync($"/api/v1/orderrounds/{created.Id}/items", itemRequest);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<OrderItemResponse>();
        Assert.NotNull(item);
        Assert.Equal("Coffee", item.Description);
        Assert.Equal(3.50m, item.Price);
    }

    [Fact]
    public async Task GetOrderRounds_Unauthenticated_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/v1/orderrounds");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
