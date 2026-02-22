using System.Threading.RateLimiting;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.HttpClients;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.Infrastructure;
using Hangfire;
using Hangfire.PostgreSql;
using HiveOrders.Api.Features.Auth;
using HiveOrders.Api.Features.Bot;
using HiveOrders.Api.Features.Jobs;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Features.Payments;
using HiveOrders.Api.Features.Notifications;
using HiveOrders.Api.Features.RecurringOrders;
using HiveOrders.Api.Shared.Events;
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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

var skipEmailConfirmation = string.Equals(
    builder.Configuration["Testing:SkipEmailConfirmation"],
    "true",
    StringComparison.OrdinalIgnoreCase);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = !skipEmailConfirmation;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "HiveOrders";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "HiveOrders";

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = "External";
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
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
})
.AddCookie("External");

AddGoogleAuthIfConfigured(authBuilder, builder);
AddMicrosoftAuthIfConfigured(authBuilder, builder);

var corsSection = builder.Configuration.GetSection("Cors:AllowedOrigins");
var corsOrigins = corsSection.Get<string[]>() ?? corsSection.GetChildren().Select(c => c.Value!).Where(v => !string.IsNullOrEmpty(v)).ToArray();
if (corsOrigins.Length == 0)
{
    if (builder.Environment.IsDevelopment())
        corsOrigins = ["http://localhost:5173", "http://localhost:3000"];
    else
        throw new InvalidOperationException("Cors:AllowedOrigins must be configured in production.");
}
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var skipRateLimiting = string.Equals(builder.Configuration["Testing:SkipRateLimiting"], "true", StringComparison.OrdinalIgnoreCase);
if (!skipRateLimiting)
{
    var permitLimit = int.TryParse(builder.Configuration["RateLimiting:Auth:PermitLimit"], out var pl) ? pl : 20;
    var windowMinutes = int.TryParse(builder.Configuration["RateLimiting:Auth:WindowMinutes"], out var wm) ? wm : 1;
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.PermitLimit = permitLimit;
            limiter.Window = TimeSpan.FromMinutes(windowMinutes);
            limiter.QueueLimit = 0;
        });
    });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddScoped<IExceptionReportService, ExceptionReportService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IOrderRoundHandler, OrderRoundHandler>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IRecurringOrderHandler, RecurringOrderHandler>();
builder.Services.AddScoped<IBotUserResolver, BotUserResolver>();
builder.Services.AddScoped<IBotLinkService, BotLinkService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddSingleton<INotificationHubClient, NotificationHubClient>();
builder.Services.AddScoped<CreateRoundsFromTemplatesJob>();
builder.Services.AddScoped<DeadlineReminderJob>();

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

builder.Services.AddSignalR();
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
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHangfireDashboard("/hangfire");
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

await app.RunAsync();

public partial class Program
{
    private Program() { }

    private static string GetFrontendRedirectUri(WebApplicationBuilder webBuilder) =>
        webBuilder.Configuration["Authentication:Google:FrontendRedirectUri"]
        ?? webBuilder.Configuration["Authentication:Microsoft:FrontendRedirectUri"]
        ?? "http://localhost:5173";

    private static async Task HandleSsoTicketReceived(
        Microsoft.AspNetCore.Authentication.TicketReceivedContext ctx,
        WebApplicationBuilder webBuilder)
    {
        var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var jwtService = ctx.HttpContext.RequestServices.GetRequiredService<JwtTokenService>();
        var frontendRedirect = GetFrontendRedirectUri(webBuilder);
        var email = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? ctx.Principal?.FindFirst("email")?.Value;
        var name = ctx.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? ctx.Principal?.FindFirst("name")?.Value ?? email ?? "User";

        if (string.IsNullOrEmpty(email))
        {
            ctx.Response.Redirect($"{frontendRedirect}/login#error=sso_no_email");
            ctx.HandleResponse();
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var defaultTenant = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == DbInitializer.DefaultTenantSlug)
                ?? throw new InvalidOperationException("Default tenant not configured.");
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Company = name,
                TenantId = defaultTenant.Id
            };
            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                ctx.Response.Redirect($"{frontendRedirect}/login#error=sso_create_failed");
                ctx.HandleResponse();
                return;
            }
            await userManager.AddToRoleAsync(user, DbInitializer.RoleUser);
        }

        var roles = await userManager.GetRolesAsync(user);
        var token = jwtService.GenerateToken(user, roles);
        ctx.Response.Redirect($"{frontendRedirect}/login#token={Uri.EscapeDataString(token)}");
        ctx.HandleResponse();
    }

    private static void AddGoogleAuthIfConfigured(
        Microsoft.AspNetCore.Authentication.AuthenticationBuilder authBuilder,
        WebApplicationBuilder webBuilder)
    {
        var googleClientId = webBuilder.Configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId) || webBuilder.Environment.IsDevelopment())
            return;

        authBuilder.AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = webBuilder.Configuration["Authentication:Google:ClientSecret"] ?? string.Empty;
            options.SignInScheme = "External";
            options.Events.OnTicketReceived = ctx => HandleSsoTicketReceived(ctx, webBuilder);
        });
    }

    private static void AddMassTransit(IServiceCollection services, IConfiguration configuration)
    {
        var awsRegion = configuration["AWS:Region"];
        var rabbitHost = configuration["RabbitMQ:Host"];

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderRoundCreatedConsumer>();
            x.AddConsumer<OrderItemAddedConsumer>();
            x.AddConsumer<OrderRoundClosedConsumer>();
            x.AddConsumer<PaymentCompletedConsumer>();

            if (!string.IsNullOrWhiteSpace(awsRegion))
                x.UsingAmazonSqs((ctx, cfg) => { ConfigureAmazonSqs(cfg, awsRegion, configuration); cfg.ConfigureEndpoints(ctx); });
            else if (!string.IsNullOrWhiteSpace(rabbitHost))
                x.UsingRabbitMq((ctx, cfg) => { ConfigureRabbitMq(cfg, rabbitHost, configuration); cfg.ConfigureEndpoints(ctx); });
            else
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
        });
    }

    private static void ConfigureAmazonSqs(
        MassTransit.IAmazonSqsBusFactoryConfigurator cfg,
        string awsRegion,
        IConfiguration configuration)
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

    private static void ConfigureRabbitMq(
        MassTransit.IRabbitMqBusFactoryConfigurator cfg,
        string rabbitHost,
        IConfiguration configuration)
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(configuration["RabbitMQ:Password"] ?? "guest");
        });
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

    private static void AddMicrosoftAuthIfConfigured(
        Microsoft.AspNetCore.Authentication.AuthenticationBuilder authBuilder,
        WebApplicationBuilder webBuilder)
    {
        var microsoftClientId = webBuilder.Configuration["Authentication:Microsoft:ClientId"];
        if (string.IsNullOrWhiteSpace(microsoftClientId) || webBuilder.Environment.IsDevelopment())
            return;

        authBuilder.AddMicrosoftAccount(options =>
        {
            options.ClientId = microsoftClientId;
            options.ClientSecret = webBuilder.Configuration["Authentication:Microsoft:ClientSecret"] ?? string.Empty;
            options.SignInScheme = "External";
            options.Events.OnTicketReceived = ctx => HandleSsoTicketReceived(ctx, webBuilder);
        });
    }
}
