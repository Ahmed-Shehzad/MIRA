using HiveOrders.Api.Shared.ValueObjects;

namespace HiveOrders.Api.Features.Admin;

public interface IAdminService
{
    Task<IReadOnlyList<AdminUserResponse>> GetUsersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TenantResponse>> GetTenantsAsync(CancellationToken cancellationToken = default);

    Task<bool> AssignAdminAsync(UserId userId, CancellationToken cancellationToken = default);
}
