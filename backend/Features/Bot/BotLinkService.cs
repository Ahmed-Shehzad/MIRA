using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;

namespace HiveOrders.Api.Features.Bot;

public class BotLinkService : IBotLinkService
{
    private readonly ApplicationDbContext _db;

    private const int CodeLength = 6;
    private static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(5);

    public BotLinkService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<string> CreateLinkCodeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var code = GenerateCode();
        var expiry = DateTime.UtcNow.Add(CodeExpiry);

        _db.Set<BotLinkCode>().Add(new BotLinkCode
        {
            Code = code,
            UserId = userId,
            ExpiresAt = expiry
        });
        await _db.SaveChangesAsync(cancellationToken);

        return code;
    }

    public async Task<bool> ConsumeLinkCodeAsync(string code, string externalId, CancellationToken cancellationToken = default)
    {
        var linkCode = await _db.Set<BotLinkCode>()
            .FirstOrDefaultAsync(c => c.Code == code && c.ExpiresAt > DateTime.UtcNow, cancellationToken);

        if (linkCode == null)
            return false;

        var user = await _db.Users.FindAsync([linkCode.UserId], cancellationToken);
        if (user == null)
            return false;

        var existing = await _db.BotUserConnections.FirstOrDefaultAsync(c => c.TenantId == user.TenantId && c.ExternalId == externalId, cancellationToken);
        if (existing != null)
            _db.BotUserConnections.Remove(existing);

        _db.BotUserConnections.Add(new BotUserConnection
        {
            TenantId = user.TenantId,
            UserId = linkCode.UserId,
            ExternalId = externalId
        });
        _db.Set<BotLinkCode>().Remove(linkCode);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string GenerateCode()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var num = Math.Abs(BitConverter.ToInt32(bytes, 0)) % (int)Math.Pow(10, CodeLength);
        return num.ToString($"D{CodeLength}");
    }
}

public class BotLinkCode
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
