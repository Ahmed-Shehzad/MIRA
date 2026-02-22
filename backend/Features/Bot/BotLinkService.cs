using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.ValueObjects;

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
            UserId = new UserId(userId),
            ExpiresAt = expiry
        });
        await _db.SaveChangesAsync(cancellationToken);

        return code.Value;
    }

    public async Task<bool> ConsumeLinkCodeAsync(string code, string externalId, CancellationToken cancellationToken = default)
    {
        var linkCodeValue = new LinkCode(code);
        var linkCode = await _db.Set<BotLinkCode>()
            .FirstOrDefaultAsync(c => c.Code == linkCodeValue && c.ExpiresAt > DateTime.UtcNow, cancellationToken);

        if (linkCode == null)
            return false;

        var user = await _db.Users.FindAsync([linkCode.UserId], cancellationToken);
        if (user == null)
            return false;

        var extId = new ExternalId(externalId);
        var existing = await _db.BotUserConnections.FirstOrDefaultAsync(c => c.TenantId == user.TenantId && c.ExternalId == extId, cancellationToken);
        if (existing != null)
            _db.BotUserConnections.Remove(existing);

        _db.BotUserConnections.Add(new BotUserConnection
        {
            TenantId = user.TenantId,
            UserId = linkCode.UserId,
            ExternalId = extId
        });
        _db.Set<BotLinkCode>().Remove(linkCode);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static LinkCode GenerateCode()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var num = Math.Abs(BitConverter.ToInt32(bytes, 0)) % (int)Math.Pow(10, CodeLength);
        return new LinkCode(num.ToString($"D{CodeLength}"));
    }
}

public class BotLinkCode
{
    public int Id { get; set; }
    public LinkCode Code { get; set; }
    public UserId UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}
