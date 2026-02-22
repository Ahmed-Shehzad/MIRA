using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.Auth;

/// <summary>Request for test token endpoint. Only available when Testing:UseLocalJwt=true.</summary>
public record TestTokenRequest(
    [property: JsonPropertyName("email")] [Required][EmailAddress] string Email,
    [property: JsonPropertyName("company")] [Required][MaxLength(200)] string Company);
