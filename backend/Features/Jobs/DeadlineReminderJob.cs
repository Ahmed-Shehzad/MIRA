using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Features.Notifications;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;

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
            var userIds = round.OrderItems.Select(i => i.UserId).Distinct().ToHashSet();
            userIds.Add(round.CreatedByUserId);

            var title = $"Order reminder: {round.RestaurantName} closes soon";
            var body = $"The order round for {round.RestaurantName} closes in about {minutesBefore} minutes. Deadline: {round.Deadline:g} UTC.";

            foreach (var userId in userIds)
            {
                db.Notifications.Add(new Notification
                {
                    TenantId = round.TenantId,
                    UserId = userId,
                    Type = NotificationTypeDeadlineReminder,
                    Title = title,
                    Body = body
                });

                var user = round.OrderItems.Select(i => i.User).FirstOrDefault(u => u.Id == userId)
                    ?? round.CreatedByUser;
                if (user?.Email != null)
                {
                    try
                    {
                        await emailService.SendEmailAsync(user.Email, title, $"<p>{body}</p>", cancellationToken);
                    }
                    catch
                    {
                        // Log and continue
                    }
                }
                try
                {
                    await pushService.SendToUserAsync(round.TenantId, userId, title, body, cancellationToken);
                }
                catch
                {
                    // Log and continue
                }
                try
                {
                    await hubClient.SendToUserAsync(round.TenantId, userId, NotificationTypeDeadlineReminder, title, body, cancellationToken);
                }
                catch
                {
                    // Log and continue
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
