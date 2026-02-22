using Amazon;
using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using HiveOrders.WsiWorker;
using MassTransit;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAWSService<IAmazonS3>(new AWSOptions
{
    Region = RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"] ?? "us-east-1"),
});

builder.Logging.ClearProviders();
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Logging.AddSerilog();

var configuration = builder.Configuration;
var awsRegion = configuration["AWS:Region"];
var serviceUrl = configuration["AWS:ServiceUrl"];
var useSqs = !string.IsNullOrWhiteSpace(awsRegion) || !string.IsNullOrWhiteSpace(serviceUrl);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<WsiAnalysisWorkerConsumer>();

    if (useSqs)
    {
        x.UsingAmazonSqs((ctx, cfg) =>
        {
            var region = configuration["AWS:Region"] ?? "us-east-1";
            var url = configuration["AWS:ServiceUrl"];
            var useLocalStack = !string.IsNullOrWhiteSpace(url);

            if (useLocalStack)
            {
                var baseUrl = url!.TrimEnd('/');
                cfg.Host(new Uri($"amazonsqs://{baseUrl}"), h =>
                {
                    h.AccessKey(configuration["AWS:AccessKey"] ?? "test");
                    h.SecretKey(configuration["AWS:SecretKey"] ?? "test");
                    h.Config(new Amazon.SQS.AmazonSQSConfig
                    {
                        ServiceURL = url,
                        AuthenticationRegion = region,
                    });
                    h.Config(new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceConfig
                    {
                        ServiceURL = url,
                        AuthenticationRegion = region,
                    });
                });
            }
            else
            {
                cfg.Host(region, h =>
                {
                    var accessKey = configuration["AWS:AccessKey"];
                    if (!string.IsNullOrEmpty(accessKey))
                        h.AccessKey(accessKey);
                    var secretKey = configuration["AWS:SecretKey"];
                    if (!string.IsNullOrEmpty(secretKey))
                        h.SecretKey(secretKey);
                    var scope = configuration["AWS:Scope"];
                    if (!string.IsNullOrEmpty(scope))
                        h.Scope(scope, false);
                });
            }

            cfg.ConfigureEndpoints(ctx);
            cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10)));
        });
    }
    else
    {
        x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
    }
});

await builder.Build().RunAsync();
