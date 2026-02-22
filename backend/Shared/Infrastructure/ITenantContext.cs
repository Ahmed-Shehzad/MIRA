using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Shared.Infrastructure;

public interface ITenantContext
{
    TenantId? TenantId { get; }
    bool IsAdmin { get; }
}
