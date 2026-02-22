using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Shared.ValueObjects;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Features.Payments;
using HiveOrders.Api.Features.Bot;
using HiveOrders.Api.Features.Notifications;
using HiveOrders.Api.Features.RecurringOrders;
using HiveOrders.Api.Features.Wsi;
using HiveOrders.Api.Shared.Sagas;

namespace HiveOrders.Api.Shared.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<OrderRound> OrderRounds => Set<OrderRound>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RecurringOrderTemplate> RecurringOrderTemplates => Set<RecurringOrderTemplate>();
    public DbSet<BotUserConnection> BotUserConnections => Set<BotUserConnection>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<WsiUpload> WsiUploads => Set<WsiUpload>();
    public DbSet<WsiJob> WsiJobs => Set<WsiJob>();
    public DbSet<WsiAnalysisSagaState> WsiAnalysisSagaStates => Set<WsiAnalysisSagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.Entity<WsiAnalysisSagaState>(e =>
        {
            e.HasKey(x => x.CorrelationId);
            e.Property(x => x.CurrentState).HasMaxLength(64);
            e.Property(x => x.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(u => u.Email).HasConversion(v => v.Value, v => new Email(v));
            e.Property(u => u.Groups)
                .HasConversion(
                    v => string.Join(',', v.Select(g => g.Value)),
                    v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => new UserGroup(s)).ToList())
                .Metadata.SetValueComparer(new UserGroupListComparer());
            e.HasOne(u => u.Tenant)
                .WithMany()
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(u => u.Email);
            e.HasIndex(u => u.CognitoUsername);
        });

        modelBuilder.Entity<Tenant>(e =>
        {
            e.Property(t => t.Slug).HasConversion(v => v.Value, v => new TenantSlug(v));
            e.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<OrderRound>(e =>
        {
            e.Property(o => o.CreatedByUserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(o => o.Status).HasConversion(v => StatusToInt(v), v => IntToOrderRoundStatus(v));
            e.HasIndex(o => o.TenantId);
            e.HasOne(o => o.CreatedByUser)
                .WithMany()
                .HasForeignKey(o => o.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.UserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(i => i.Price).HasConversion(v => v.Value, v => new Money(v));
            e.HasOne(i => i.OrderRound)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(i => i.OrderRoundId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.UserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(p => p.StripePaymentIntentId).HasConversion(v => v.Value, v => new StripePaymentIntentId(v));
            e.Property(p => p.Amount).HasConversion(v => v.Value, v => new Money(v));
            e.Property(p => p.Status).HasConversion(v => StatusToInt(v), v => IntToPaymentStatus(v));
            e.HasIndex(p => p.TenantId);
            e.HasOne(p => p.OrderRound)
                .WithMany()
                .HasForeignKey(p => p.OrderRoundId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecurringOrderTemplate>(e =>
        {
            e.Property(t => t.CreatedByUserId).HasConversion(v => v.Value, v => new UserId(v));
            e.HasIndex(t => t.TenantId);
            e.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BotUserConnection>(e =>
        {
            e.Property(c => c.UserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(c => c.ExternalId).HasConversion(v => v.Value, v => new ExternalId(v));
            e.HasIndex(c => new { c.TenantId, c.ExternalId }).IsUnique();
        });

        modelBuilder.Entity<BotLinkCode>(e =>
        {
            e.Property(c => c.Code).HasConversion(v => v.Value, v => new LinkCode(v));
            e.Property(c => c.UserId).HasConversion(v => v.Value, v => new UserId(v));
            e.HasIndex(c => c.Code);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.Property(n => n.UserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(n => n.Type).HasConversion(v => v.Value, v => new NotificationType(v));
            e.HasIndex(n => new { n.TenantId, n.UserId, n.CreatedAt });
        });

        modelBuilder.Entity<PushSubscription>(e =>
        {
            e.Property(s => s.UserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(s => s.Endpoint).HasConversion(v => v.Value, v => new PushEndpoint(v));
            e.HasIndex(s => new { s.TenantId, s.UserId, s.Endpoint }).IsUnique();
        });

        modelBuilder.Entity<WsiUpload>(e =>
        {
            e.ToTable("WsiUploads");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasConversion(v => v.Value, v => new WsiUploadId(v));
            e.Property(u => u.UploadedByUserId).HasConversion(v => v.Value, v => new UserId(v));
            e.HasIndex(u => u.TenantId);
            e.HasOne(u => u.UploadedByUser)
                .WithMany()
                .HasForeignKey(u => u.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WsiJob>(e =>
        {
            e.ToTable("WsiJobs");
            e.HasKey(j => j.Id);
            e.Property(j => j.Id).HasConversion(v => v.Value, v => new WsiJobId(v));
            e.Property(j => j.WsiUploadId).HasConversion(v => v.Value, v => new WsiUploadId(v));
            e.Property(j => j.RequestedByUserId).HasConversion(v => v.Value, v => new UserId(v));
            e.Property(j => j.Status).HasConversion(v => v.Value, v => ParseWsiJobStatus(v));
            e.HasIndex(j => j.TenantId);
            e.HasOne(j => j.WsiUpload)
                .WithMany(u => u.Jobs)
                .HasForeignKey(j => j.WsiUploadId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(j => j.RequestedByUser)
                .WithMany()
                .HasForeignKey(j => j.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static WsiJobStatus ParseWsiJobStatus(string v)
    {
        if (v == "Processing") return WsiJobStatus.Processing;
        if (v == "Completed") return WsiJobStatus.Completed;
        if (v == "Failed") return WsiJobStatus.Failed;
        return WsiJobStatus.Pending;
    }

    private static int StatusToInt(OrderRoundStatus s) => s.Value == "Closed" ? 1 : 0;
    private static int StatusToInt(PaymentStatus s)
    {
        if (s.Value == "Completed") return 1;
        if (s.Value == "Failed") return 2;
        return 0;
    }

    private static OrderRoundStatus IntToOrderRoundStatus(int v) => v == 1 ? OrderRoundStatus.Closed : OrderRoundStatus.Open;
    private static PaymentStatus IntToPaymentStatus(int v)
    {
        if (v == 1) return PaymentStatus.Completed;
        if (v == 2) return PaymentStatus.Failed;
        return PaymentStatus.Pending;
    }

    private sealed class UserGroupListComparer : ValueComparer<IList<UserGroup>>
    {
        public UserGroupListComparer() : base(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.Value.GetHashCode())),
            c => c.ToList())
        { }
    }
}
