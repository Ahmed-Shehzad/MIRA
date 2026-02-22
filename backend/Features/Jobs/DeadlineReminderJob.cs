using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Features.Notifications;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;
using HiveOrders.Api.Shared.ValueObjects;
using OrderRoundStatus = HiveOrders.Api.Shared.ValueObjects.OrderRoundStatus;

namespace HiveOrders.Api.Features.Jobs;

public class DeadlineReminderJob
{
    private const string NotificationTypeDeadlineReminder = "DeadlineReminder";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public DeadlineReminderJob(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var minutesBefore = int.TryParse(_configuration["Notifications:ReminderMinutesBeforeDeadline"], out var m) ? m : 60;
        var reminderWindowStart = DateTime.UtcNow.AddMinutes(minutesBefore).AddMinutes(-5);
        var reminderWindowEnd = DateTime.UtcNow.AddMinutes(minutesBefore).AddMinutes(5);

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
        var hubClient = scope.ServiceProvider.GetRequiredService<INotificationHubClient>();

        var rounds = await db.OrderRounds
            .Include(o => o.OrderItems)
            .ThenInclude(i => i.User)
            .Include(o => o.CreatedByUser)
            .Where(o => o.Status == OrderRoundStatus.Open
                && o.Deadline >= reminderWindowStart
                && o.Deadline <= reminderWindowEnd)
            .ToListAsync(cancellationToken);

        foreach (var round in rounds)
        {
            var title = $"Order reminder: {round.RestaurantName} closes soon";
            var body = $"The order round for {round.RestaurantName} closes in about {minutesBefore} minutes. Deadline: {round.Deadline:g} UTC.";
            var userIds = round.OrderItems.Select(i => i.UserId).Distinct().ToHashSet();
            userIds.Add(round.CreatedByUserId);

            foreach (var userId in userIds)
            {
                db.Notifications.Add(new Notification
                {
                    TenantId = round.TenantId,
                    UserId = userId,
                    Type = (NotificationType)NotificationTypeDeadlineReminder,
                    Title = title,
                    Body = body
                });
                await SendReminderToUserAsync(new ReminderContext(
                    round, userId, title, body,
                    emailService, pushService, hubClient),
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record ReminderContext(
        OrderRound Round,
        UserId UserId,
        string Title,
        string Body,
        IEmailService EmailService,
        IPushNotificationService PushService,
        INotificationHubClient HubClient);

    private static async Task SendReminderToUserAsync(ReminderContext ctx, CancellationToken cancellationToken)
    {
        var user = ctx.Round.OrderItems.Select(i => i.User).FirstOrDefault(u => u.Id == ctx.UserId)
            ?? ctx.Round.CreatedByUser;
        if (user?.Email.Value != null)
        {
            try
            {
                await ctx.EmailService.SendEmailAsync(user.Email.Value, ctx.Title, $"<p>{ctx.Body}</p>", cancellationToken);
            }
            catch
            {
                // Log and continue
            }
        }
        try
        {
            await ctx.PushService.SendToUserAsync(ctx.Round.TenantId, ctx.UserId.Value, ctx.Title, ctx.Body, cancellationToken);
        }
        catch
        {
            // Log and continue
        }
        try
        {
            await ctx.HubClient.SendToUserAsync(ctx.Round.TenantId, ctx.UserId.Value, NotificationTypeDeadlineReminder, ctx.Title, ctx.Body, cancellationToken);
        }
        catch
        {
            // Log and continue
        }
    }
}
