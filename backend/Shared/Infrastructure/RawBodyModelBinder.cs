using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HiveOrders.Api.Shared.Infrastructure;

public sealed class RawBodyModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var request = bindingContext.HttpContext.Request;
        request.EnableBuffering();

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(bindingContext.HttpContext.RequestAborted);
        request.Body.Position = 0;

        bindingContext.Result = ModelBindingResult.Success(body);
    }
}
