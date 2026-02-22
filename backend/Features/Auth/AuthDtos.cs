using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.Auth;

public record RegisterRequest(
    [property: JsonPropertyName("email")]
    [Required][EmailAddress] string Email,
    [property: JsonPropertyName("password")]
    [Required][MinLength(6)] string Password,
    [property: JsonPropertyName("company")]
    [Required][MaxLength(200)] string Company);

public record LoginRequest(
    [property: JsonPropertyName("email")]
    [Required][EmailAddress] string Email,
    [property: JsonPropertyName("password")]
    [Required] string Password);

public record AuthResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("company")] string Company,
    [property: JsonPropertyName("roles")] IList<string> Roles);
