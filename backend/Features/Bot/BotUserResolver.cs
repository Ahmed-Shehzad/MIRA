using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;

namespace HiveOrders.Api.Features.Bot;

public class BotUserResolver : IBotUserResolver
{
    private readonly ApplicationDbContext _db;

    public BotUserResolver(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string?> ResolveUserIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        var connection = await _db.Set<BotUserConnection>()
            .FirstOrDefaultAsync(c => c.ExternalId == externalId, cancellationToken);
        return connection?.UserId;
    }
}

public class BotUserConnection
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public required string UserId { get; set; }
    public required string ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
