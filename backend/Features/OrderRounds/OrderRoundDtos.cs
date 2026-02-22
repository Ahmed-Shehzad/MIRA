using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.OrderRounds;

public record CreateOrderRoundRequest(
    [property: JsonPropertyName("restaurantName")]
    [Required][MaxLength(200)] string RestaurantName,
    [property: JsonPropertyName("restaurantUrl")]
    [MaxLength(500)] string? RestaurantUrl,
    [property: JsonPropertyName("deadline")]
    [Required][property: JsonRequired] DateTime Deadline);

public record UpdateOrderRoundRequest(
    [property: JsonPropertyName("restaurantName")]
    [MaxLength(200)] string? RestaurantName,
    [property: JsonPropertyName("restaurantUrl")]
    [MaxLength(500)] string? RestaurantUrl,
    [property: JsonPropertyName("deadline")]
    DateTime? Deadline,
    [property: JsonPropertyName("close")]
    bool? Close);

public record OrderRoundResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("restaurantName")] string RestaurantName,
    [property: JsonPropertyName("restaurantUrl")] string? RestaurantUrl,
    [property: JsonPropertyName("createdByUserId")] string CreatedByUserId,
    [property: JsonPropertyName("deadline")] DateTime Deadline,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("itemCount")] int ItemCount);

public record CreateOrderItemRequest(
    [property: JsonPropertyName("description")]
    [Required][MaxLength(500)] string Description,
    [property: JsonPropertyName("price")]
    [Range(0, double.MaxValue)][property: JsonRequired] decimal Price,
    [property: JsonPropertyName("notes")]
    [MaxLength(500)] string? Notes);

public record UpdateOrderItemRequest(
    [property: JsonPropertyName("description")]
    [MaxLength(500)] string? Description,
    [property: JsonPropertyName("price")]
    [Range(0, double.MaxValue)] decimal? Price,
    [property: JsonPropertyName("notes")]
    [MaxLength(500)] string? Notes);

public record OrderItemResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("orderRoundId")] int OrderRoundId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userEmail")] string UserEmail,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("notes")] string? Notes);

public record OrderRoundDetailResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("restaurantName")] string RestaurantName,
    [property: JsonPropertyName("restaurantUrl")] string? RestaurantUrl,
    [property: JsonPropertyName("createdByUserId")] string CreatedByUserId,
    [property: JsonPropertyName("createdByUserEmail")] string CreatedByUserEmail,
    [property: JsonPropertyName("deadline")] DateTime Deadline,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("items")] IReadOnlyList<OrderItemResponse> Items);
