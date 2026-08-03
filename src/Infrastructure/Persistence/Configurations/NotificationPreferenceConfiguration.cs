using Kart.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();

        builder.Property(x => x.OptOutMatrixJson)
            .HasColumnName("opt_out_matrix")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(x => x.AppInstalled).HasColumnName("app_installed").HasDefaultValue(false);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.LastUpdatedAt).HasColumnName("last_updated_at").HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasDefaultValue("system:notification-preference-sync-consumer").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasDefaultValue("system:notification-preference-sync-consumer").IsRequired();
    }
}
