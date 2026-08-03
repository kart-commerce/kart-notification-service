using Kart.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Notification.Infrastructure.Persistence.Configurations;

public sealed class TrackingOrderIndexConfiguration : IEntityTypeConfiguration<TrackingOrderIndex>
{
    public void Configure(EntityTypeBuilder<TrackingOrderIndex> builder)
    {
        builder.ToTable("tracking_order_index");

        builder.HasKey(x => x.TrackingId);
        builder.Property(x => x.TrackingId).HasColumnName("tracking_id").ValueGeneratedNever();

        builder.Property(x => x.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasDefaultValue("system:notification-tracking-order-index-consumer").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasDefaultValue("system:notification-tracking-order-index-consumer").IsRequired();
    }
}
