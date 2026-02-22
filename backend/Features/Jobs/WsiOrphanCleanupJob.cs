using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Features.Wsi;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;

namespace HiveOrders.Api.Features.Jobs;

/// <summary>Deletes WSI uploads stuck in Uploading status for more than 24 hours and their orphaned S3 objects.</summary>
public class WsiOrphanCleanupJob
{
    private static readonly TimeSpan OrphanThreshold = TimeSpan.FromHours(24);

    private readonly IServiceProvider _serviceProvider;

    public WsiOrphanCleanupJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - OrphanThreshold;

        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var s3Service = scope.ServiceProvider.GetRequiredService<IS3PresignedUrlService>();

        var orphans = await db.WsiUploads
            .Where(u => u.Status == WsiUploadStatusValues.Uploading && u.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var upload in orphans)
        {
            await s3Service.TryDeleteObjectAsync(upload.S3Key, cancellationToken);
            db.WsiUploads.Remove(upload);
        }

        if (orphans.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }
}
