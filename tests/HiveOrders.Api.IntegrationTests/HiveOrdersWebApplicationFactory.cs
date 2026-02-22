using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HiveOrders.Api.Shared.Infrastructure;

namespace HiveOrders.Api.IntegrationTests;

public sealed class HiveOrdersWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "TestKey-MustBeAtLeast32CharactersLong!";

    public HiveOrdersWebApplicationFactory()
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "HiveOrders",
                ["Jwt:Audience"] = "HiveOrders",
                ["Testing:SkipRateLimiting"] = "true",
                ["Testing:UseLocalJwt"] = "true",
                ["AWS:Region"] = "us-east-1"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null)
                services.Remove(emailDescriptor);

            services.AddSingleton<IEmailService, NoOpEmailService>();
        });
    }
}

file class NoOpEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
