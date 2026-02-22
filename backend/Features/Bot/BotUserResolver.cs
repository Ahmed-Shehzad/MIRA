using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.ValueObjects;

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
        var extId = new ExternalId(externalId);
        var connection = await _db.Set<BotUserConnection>()
            .FirstOrDefaultAsync(c => c.ExternalId == extId, cancellationToken);
        return connection?.UserId.Value;
    }
}

public class BotUserConnection
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public UserId UserId { get; set; }
    public ExternalId ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
