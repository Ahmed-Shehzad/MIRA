using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Features.RecurringOrders;
using HiveOrders.Api.Shared.Data;
using Cronos;

namespace HiveOrders.Api.Features.Jobs;

public class CreateRoundsFromTemplatesJob
{
    private readonly IServiceProvider _serviceProvider;

    public CreateRoundsFromTemplatesJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var templates = await db.RecurringOrderTemplates
            .Where(t => t.IsActive)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var template in templates)
        {
            try
            {
                if (!CronExpression.TryParse(template.CronExpression, CronFormat.Standard, out var cron))
                    continue;

                var from = now.AddMinutes(-2);
                var next = cron.GetNextOccurrence(from, TimeZoneInfo.Utc);
                if (next == null || next > now.AddMinutes(1))
                    continue;

                var deadline = now.AddHours(8);
                var round = new OrderRound
                {
                    TenantId = template.TenantId,
                    RestaurantName = template.RestaurantName,
                    RestaurantUrl = template.RestaurantUrl,
                    CreatedByUserId = template.CreatedByUserId,
                    Deadline = deadline,
                    Status = OrderRoundStatus.Open
                };

                db.OrderRounds.Add(round);
                template.NextRunAt = cron.GetNextOccurrence(now.AddMinutes(1), TimeZoneInfo.Utc);
            }
            catch
            {
                // Log and continue with other templates
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
