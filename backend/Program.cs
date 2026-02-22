using System.Threading.RateLimiting;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.HttpClients;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using HiveOrders.Api.Features.Admin;
using HiveOrders.Api.Features.Auth;
using HiveOrders.Api.Features.Bot;
using HiveOrders.Api.Features.Jobs;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Features.Payments;
using HiveOrders.Api.Features.Notifications;
using HiveOrders.Api.Features.RecurringOrders;
using HiveOrders.Api.Features.Storage;
using HiveOrders.Api.Features.Wsi;
using HiveOrders.Api.Shared.Events;
using HiveOrders.Api.Shared.Sagas;
using MassTransit;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Options;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection not configured");

var awsRegion = builder.Configuration["AWS:Cognito:Region"] ?? builder.Configuration["AWS:Region"];
if (string.IsNullOrWhiteSpace(awsRegion)) awsRegion = "us-east-1";
builder.Services.AddAWSService<IAmazonCognitoIdentityProvider>(new AWSOptions { Region = RegionEndpoint.GetBySystemName(awsRegion) });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

var useLocalJwt = string.Equals(builder.Configuration["Testing:UseLocalJwt"], "true", StringComparison.OrdinalIgnoreCase);
var skipRateLimiting = string.Equals(builder.Configuration["Testing:SkipRateLimiting"], "true", StringComparison.OrdinalIgnoreCase);
ConfigureAuth(builder.Services, builder.Configuration, useLocalJwt);
ConfigureCors(builder.Services, builder.Configuration, builder.Environment);
ConfigureRateLimiting(builder.Services, builder.Configuration, skipRateLimiting);

builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IExceptionToStatusCodeMapper, ExceptionToStatusCodeMapper>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<IExceptionReportService, ExceptionReportService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ICognitoUserService, CognitoUserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOrderRoundHandler, OrderRoundHandler>();
builder.Services.AddScoped<IStripePaymentIntentClient, StripePaymentIntentClient>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IStripeWebhookHandler, StripeWebhookHandler>();
builder.Services.AddScoped<IRecurringOrderHandler, RecurringOrderHandler>();
builder.Services.AddScoped<IStorageHandler, StorageHandler>();
builder.Services.AddScoped<IBotUserResolver, BotUserResolver>();
builder.Services.AddScoped<IBotLinkService, BotLinkService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<IWsiHandler, WsiHandler>();
builder.Services.AddSingleton<IS3PresignedUrlService, S3PresignedUrlService>();
builder.Services.AddSingleton<IApiGatewayWebSocketPushService, ApiGatewayWebSocketPushService>();
builder.Services.AddSingleton<INotificationHubClient, NotificationHubClient>();
builder.Services.AddScoped<CreateRoundsFromTemplatesJob>();
builder.Services.AddScoped<DeadlineReminderJob>();
builder.Services.AddScoped<WsiOrphanCleanupJob>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

AddMassTransit(builder.Services, builder.Configuration);

builder.Services.AddHttpClient();
AddTeamsBotIfConfigured(builder.Services);

AddRefitClients(builder.Services, builder.Configuration);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddControllers();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.InitializeAsync(app.Services);
}

using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<CreateRoundsFromTemplatesJob>(
        "create-rounds-from-templates",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Minutely);
    recurringJobManager.AddOrUpdate<DeadlineReminderJob>(
        "deadline-reminders",
        job => job.ExecuteAsync(CancellationToken.None),
        "*/15 * * * *");
    recurringJobManager.AddOrUpdate<WsiOrphanCleanupJob>(
        "wsi-orphan-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<Asp.Versioning.ApiExplorer.IApiVersionDescriptionProvider>();
        foreach (var groupName in provider.ApiVersionDescriptions.Select(d => d.GroupName))
        {
            options.SwaggerEndpoint($"/swagger/{groupName}/swagger.json", groupName.ToUpperInvariant());
        }
    });
}

app.UseSerilogRequestLogging();
var useXRay = string.Equals(app.Configuration["Tracing:XRay:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
if (useXRay)
{
    app.UseXRay(app.Configuration["Tracing:XRay:ServiceName"] ?? "mira-api");
}
app.Use(async (ctx, next) =>
{
    ctx.Request.EnableBuffering();
    await next(ctx);
});
app.UseExceptionHandler();
if (!skipRateLimiting)
    app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<CognitoUserProvisioningMiddleware>();
app.UseMiddleware<TenantIdRlsMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHangfireDashboard("/hangfire");
var isTesting = app.Environment.IsEnvironment("Testing");
var healthPredicate = isTesting
    ? (Func<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration, bool>)(r => r.Name != "masstransit-bus")
    : _ => true;

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = healthPredicate });
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = healthPredicate });
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

await app.RunAsync();

public partial class Program
{
    private Program() { }

    private static void AddMassTransit(IServiceCollection services, IConfiguration configuration)
    {
        var awsRegion = configuration["AWS:Region"];
        var serviceUrl = configuration["AWS:ServiceUrl"];
        var useSqs = !string.IsNullOrWhiteSpace(awsRegion) || !string.IsNullOrWhiteSpace(serviceUrl);

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.AddConsumer<OrderRoundCreatedConsumer>();
            x.AddConsumer<OrderItemAddedConsumer>();
            x.AddConsumer<OrderRoundClosedConsumer>();
            x.AddConsumer<PaymentCompletedConsumer>();
            x.AddConsumer<WsiAnalysisRequestedConsumer>();

            x.AddSagaStateMachine<WsiAnalysisSaga, WsiAnalysisSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<ApplicationDbContext>();
                    r.UsePostgres();
                });

            x.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
            });

            if (useSqs)
            {
                x.UsingAmazonSqs((ctx, cfg) =>
                {
                    ConfigureAmazonSqs(cfg, configuration);
                    cfg.ConfigureEndpoints(ctx);
                    cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10)));
                });
            }
            else
            {
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            }
        });
    }

    private static void ConfigureAmazonSqs(
        MassTransit.IAmazonSqsBusFactoryConfigurator cfg,
        IConfiguration configuration)
    {
        var awsRegion = configuration["AWS:Region"] ?? "us-east-1";
        var serviceUrl = configuration["AWS:ServiceUrl"];
        var useLocalStack = !string.IsNullOrWhiteSpace(serviceUrl);

        if (useLocalStack)
        {
            var url = serviceUrl!.TrimEnd('/');
            cfg.Host(new Uri($"amazonsqs://{url}"), h =>
            {
                h.AccessKey(configuration["AWS:AccessKey"] ?? "test");
                h.SecretKey(configuration["AWS:SecretKey"] ?? "test");
                h.Config(new Amazon.SQS.AmazonSQSConfig
                {
                    ServiceURL = serviceUrl,
                    AuthenticationRegion = awsRegion,
                });
                h.Config(new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceConfig
                {
                    ServiceURL = serviceUrl,
                    AuthenticationRegion = awsRegion,
                });
            });
        }
        else
        {
            cfg.Host(awsRegion, h =>
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
    }

    private static void AddTeamsBotIfConfigured(IServiceCollection services)
    {
        services.AddSingleton<IBotFrameworkHttpAdapter>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<CloudAdapter>>();
            return new CloudAdapter(config.GetSection("Teams"), httpClientFactory, logger);
        });
        services.AddTransient<IBot, TeamsBot>();
    }

    private static void AddRefitClients(IServiceCollection services, IConfiguration configuration)
    {
        var externalBaseAddress = configuration["HttpClients:ExternalService:BaseAddress"];
        if (!string.IsNullOrWhiteSpace(externalBaseAddress))
            services.AddRefitClient<IExternalServiceApi>(configuration, "ExternalService");
    }

    private static void ConfigureAuth(IServiceCollection services, IConfiguration configuration, bool useLocalJwt)
    {
        if (!useLocalJwt)
        {
            var cognitoUserPoolId = configuration["AWS:Cognito:UserPoolId"];
            var cognitoRegion = configuration["AWS:Cognito:Region"] ?? configuration["AWS:Region"];
            if (string.IsNullOrWhiteSpace(cognitoUserPoolId))
                throw new InvalidOperationException("AWS:Cognito:UserPoolId is required. Auth uses Cognito only.");
            if (string.IsNullOrWhiteSpace(cognitoRegion))
                throw new InvalidOperationException("AWS:Cognito:Region or AWS:Region is required.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            ConfigureJwtBearerOptions(options, configuration, useLocalJwt);
        });
    }

    private static void ConfigureJwtBearerOptions(
        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions options,
        IConfiguration configuration,
        bool useLocalJwt)
    {
        if (useLocalJwt)
        {
            var jwtKey = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Testing:UseLocalJwt requires Jwt:Key.");
            var issuer = configuration["Jwt:Issuer"] ?? "HiveOrders.Test";
            var audience = configuration["Jwt:Audience"] ?? "HiveOrders.Test";
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey));
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                IssuerSigningKey = key
            };
        }
        else
        {
            var cognitoRegion = configuration["AWS:Cognito:Region"] ?? configuration["AWS:Region"];
            var cognitoUserPoolId = configuration["AWS:Cognito:UserPoolId"];
            var cognitoClientId = configuration["AWS:Cognito:ClientId"];
            options.Authority = $"https://cognito-idp.{cognitoRegion}.amazonaws.com/{cognitoUserPoolId}";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = !string.IsNullOrEmpty(cognitoClientId),
                ValidAudience = cognitoClientId,
                ValidateLifetime = true
            };
        }
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"].FirstOrDefault()
                    ?? ctx.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    }

    private static void ConfigureCors(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var corsSection = configuration.GetSection("Cors:AllowedOrigins");
        var corsOrigins = corsSection.Get<string[]>() ?? corsSection.GetChildren().Select(c => c.Value!).Where(v => !string.IsNullOrEmpty(v)).ToArray();
        if (corsOrigins.Length == 0)
        {
            if (environment.IsDevelopment())
                corsOrigins = ["http://localhost:5173", "http://localhost:3000"];
            else
                throw new InvalidOperationException("Cors:AllowedOrigins must be configured in production.");
        }
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(corsOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    private static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration, bool skipRateLimiting)
    {
        if (skipRateLimiting) return;
        var permitLimit = int.TryParse(configuration["RateLimiting:Auth:PermitLimit"], out var pl) ? pl : 20;
        var windowMinutes = int.TryParse(configuration["RateLimiting:Auth:WindowMinutes"], out var wm) ? wm : 1;
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("auth", limiter =>
            {
                limiter.PermitLimit = permitLimit;
                limiter.Window = TimeSpan.FromMinutes(windowMinutes);
                limiter.QueueLimit = 0;
            });
        });
    }
}
