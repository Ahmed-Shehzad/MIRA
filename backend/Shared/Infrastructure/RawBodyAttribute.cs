using Microsoft.AspNetCore.Mvc;

namespace HiveOrders.Api.Shared.Infrastructure;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RawBodyAttribute : ModelBinderAttribute
{
    public RawBodyAttribute()
        : base(typeof(RawBodyModelBinder))
    {
    }
}
