using Kart.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Notification.Infrastructure.Persistence.Configurations;

public sealed class OrderUserIndexConfiguration : IEntityTypeConfiguration<OrderUserIndex>
{
    public void Configure(EntityTypeBuilder<OrderUserIndex> builder)
    {
        builder.ToTable("order_user_index");

        builder.HasKey(x => x.OrderId);
        builder.Property(x => x.OrderId).HasColumnName("order_id").ValueGeneratedNever();

        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasDefaultValue("system:notification-order-user-index-consumer").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasDefaultValue("system:notification-order-user-index-consumer").IsRequired();
    }
}
