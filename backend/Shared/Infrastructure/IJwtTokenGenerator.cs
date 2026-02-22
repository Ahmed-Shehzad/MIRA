using HiveOrders.Api.Shared.Identity;

namespace HiveOrders.Api.Shared.Infrastructure;

public interface IJwtTokenGenerator
{
    string Generate(AppUser user);
}
