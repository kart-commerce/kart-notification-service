using Kart.Notification.Domain.Entities;
using Kart.Notification.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Kart.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<OrderUserIndex> OrderUserIndexes => Set<OrderUserIndex>();

    public DbSet<TrackingOrderIndex> TrackingOrderIndexes => Set<TrackingOrderIndex>();

    public DbSet<NotificationAuditLogEntry> AuditLogEntries => Set<NotificationAuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
    }
}
