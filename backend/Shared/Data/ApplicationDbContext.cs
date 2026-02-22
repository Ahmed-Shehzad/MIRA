using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Identity;
using HiveOrders.Api.Features.OrderRounds;
using HiveOrders.Api.Features.Payments;
using HiveOrders.Api.Features.Bot;
using HiveOrders.Api.Features.Notifications;
using HiveOrders.Api.Features.RecurringOrders;

namespace HiveOrders.Api.Shared.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<OrderRound> OrderRounds => Set<OrderRound>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RecurringOrderTemplate> RecurringOrderTemplates => Set<RecurringOrderTemplate>();
    public DbSet<BotUserConnection> BotUserConnections => Set<BotUserConnection>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderRound>()
            .HasIndex(o => o.TenantId);

        builder.Entity<OrderRound>()
            .HasOne(o => o.CreatedByUser)
            .WithMany()
            .HasForeignKey(o => o.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderItem>()
            .HasOne(i => i.OrderRound)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(i => i.OrderRoundId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<OrderItem>()
            .HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasIndex(p => p.TenantId);

        builder.Entity<Payment>()
            .HasOne(p => p.OrderRound)
            .WithMany()
            .HasForeignKey(p => p.OrderRoundId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecurringOrderTemplate>()
            .HasIndex(t => t.TenantId);

        builder.Entity<RecurringOrderTemplate>()
            .HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<BotUserConnection>()
            .HasIndex(c => new { c.TenantId, c.ExternalId })
            .IsUnique();

        builder.Entity<BotLinkCode>()
            .HasIndex(c => c.Code);

        builder.Entity<Notification>()
            .HasIndex(n => new { n.TenantId, n.UserId, n.CreatedAt });

        builder.Entity<PushSubscription>()
            .HasIndex(s => new { s.TenantId, s.UserId, s.Endpoint })
            .IsUnique();
    }
}
