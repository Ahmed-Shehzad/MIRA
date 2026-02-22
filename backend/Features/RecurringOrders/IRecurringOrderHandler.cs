using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HiveOrders.Api.Features.RecurringOrders;

public interface IRecurringOrderHandler
{
    Task<IReadOnlyList<RecurringOrderTemplateResponse>> GetMyTemplatesAsync(string userId, CancellationToken cancellationToken = default);
    Task<RecurringOrderTemplateResponse?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default);
    Task<RecurringOrderTemplateResponse?> CreateAsync(CreateRecurringOrderTemplateRequest request, string userId, CancellationToken cancellationToken = default);
    Task<RecurringOrderTemplateResponse?> UpdateAsync(int id, UpdateRecurringOrderTemplateRequest request, string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken = default);
}

public record CreateRecurringOrderTemplateRequest(
    [property: JsonPropertyName("restaurantName")]
    [Required][MaxLength(200)]
    string RestaurantName,
    [property: JsonPropertyName("restaurantUrl")]
    [MaxLength(500)]
    string? RestaurantUrl,
    [property: JsonPropertyName("cronExpression")]
    [Required][MaxLength(50)][property: JsonRequired]
    string CronExpression);

public record UpdateRecurringOrderTemplateRequest(
    [property: JsonPropertyName("restaurantName")]
    [MaxLength(200)]
    string? RestaurantName,
    [property: JsonPropertyName("restaurantUrl")]
    [MaxLength(500)]
    string? RestaurantUrl,
    [property: JsonPropertyName("cronExpression")]
    [MaxLength(50)]
    string? CronExpression,
    [property: JsonPropertyName("isActive")]
    bool? IsActive);

public record RecurringOrderTemplateResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("restaurantName")] string RestaurantName,
    [property: JsonPropertyName("restaurantUrl")] string? RestaurantUrl,
    [property: JsonPropertyName("cronExpression")] string CronExpression,
    [property: JsonPropertyName("createdByUserId")] string CreatedByUserId,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("nextRunAt")] DateTime? NextRunAt);
