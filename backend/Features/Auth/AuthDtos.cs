using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.Auth;

public record AuthResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("company")] string Company,
    [property: JsonPropertyName("groups")] IList<string> Groups);
