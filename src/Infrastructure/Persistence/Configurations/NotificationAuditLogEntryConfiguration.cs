using Kart.Notification.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Notification.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="NotificationAuditLogEntry"/> to `notification_audit_log` - a plain, independent table, not part of database-design.md's approved schema (this service registers its own concrete `IAuditLogWriter`; see that class's remarks).</summary>
public sealed class NotificationAuditLogEntryConfiguration : IEntityTypeConfiguration<NotificationAuditLogEntry>
{
    public void Configure(EntityTypeBuilder<NotificationAuditLogEntry> builder)
    {
        builder.ToTable("notification_audit_log");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.ServiceName).HasColumnName("service_name").IsRequired();
        builder.Property(e => e.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(e => e.ActorType).HasColumnName("actor_type").IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");

        builder.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("idx_notification_audit_log_entity");
    }
}
