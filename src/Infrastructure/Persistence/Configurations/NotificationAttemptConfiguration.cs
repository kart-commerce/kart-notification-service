using Kart.Notification.Domain.Entities;
using Kart.Notification.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kart.Notification.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps `notification_attempts` for EF's model (LINQ reads, e.g. a future ops audit query) - the
/// table's actual physical DDL (HASH partitioning, the status-guard trigger, RLS policies) is
/// hand-written raw SQL in the initial migration, not generated from this fluent configuration
/// (EF Core has no native `PARTITION BY HASH` support). Every actual write still goes through
/// `NotificationAttemptStore`'s raw parameterized SQL, never this configuration's change-tracked
/// `SaveChanges` path - see that class's own remarks.
/// </summary>
public sealed class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationAttempt> builder)
    {
        builder.ToTable("notification_attempts", t =>
        {
            t.HasCheckConstraint(
                "chk_notification_attempt_suppressed_reason_shape",
                "(status = 'Suppressed' AND suppressed_reason IS NOT NULL) OR (status <> 'Suppressed' AND suppressed_reason IS NULL)");
            t.HasCheckConstraint(
                "chk_notification_attempt_count_within_tier",
                "(criticality_tier = 'Tier1' AND attempt_count <= 5) OR (criticality_tier = 'Tier2' AND attempt_count <= 3) OR (criticality_tier = 'Tier3' AND attempt_count <= 2)");
        });

        builder.HasKey(x => new { x.EventId, x.Channel });

        builder.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasConversion(new ValueConverter<Channel, string>(c => c.ToDbValue(), s => ChannelExtensions.FromDbValue(s)))
            .HasMaxLength(16);

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TriggeringEventType).HasColumnName("triggering_event_type").IsRequired();

        builder.Property(x => x.CriticalityTier)
            .HasColumnName("criticality_tier")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Category).HasColumnName("category").IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(DeliveryOutcome.Pending)
            .IsRequired();

        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(x => x.SuppressedReason).HasColumnName("suppressed_reason");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.LastAttemptAt).HasColumnName("last_attempt_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("idx_notification_attempts_user_audit");
        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("idx_notification_attempts_failed")
            .HasFilter("status = 'Failed'");
    }
}
