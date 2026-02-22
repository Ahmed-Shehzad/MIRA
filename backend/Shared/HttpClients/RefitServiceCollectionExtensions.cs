using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Refit;

namespace HiveOrders.Api.Shared.HttpClients;

public static class RefitServiceCollectionExtensions
{
    public static IHttpClientBuilder AddRefitClient<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        string configKey)
        where T : class
    {
        var baseAddress = configuration[$"HttpClients:{configKey}:BaseAddress"]
            ?? throw new InvalidOperationException($"HttpClients:{configKey}:BaseAddress not configured.");

        var builder = services
            .AddRefitClient<T>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress));

        var resilienceSection = configuration.GetSection($"HttpClients:{configKey}:Resilience");
        if (resilienceSection.Exists())
            builder.AddStandardResilienceHandler(resilienceSection);
        else
            builder.AddStandardResilienceHandler();

        return builder;
    }

    public static IHttpClientBuilder AddRefitClient<T>(
        this IServiceCollection services,
        string baseAddress)
        where T : class
    {
        var builder = services
            .AddRefitClient<T>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress));
        builder.AddStandardResilienceHandler();
        return builder;
    }
}
