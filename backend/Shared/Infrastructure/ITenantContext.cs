namespace HiveOrders.Api.Shared.Infrastructure;

public interface ITenantContext
{
    int? TenantId { get; }
    bool IsAdmin { get; }
}
