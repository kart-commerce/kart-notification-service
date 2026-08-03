using Kart.Notification.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kart.Notification.Infrastructure.Persistence;

/// <summary>NOTIF-4 / ADR-0020's Write Mechanics step 0 - raw SQL upsert-on-consume seeding plus the single-hop and two-hop resolve queries.</summary>
public sealed class LookupIndexStore(NotificationDbContext dbContext) : IUserIdResolutionService
{
    public Task SeedOrderUserIndexAsync(Guid orderId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO order_user_index (order_id, user_id) VALUES ({orderId}, {userId})
             ON CONFLICT (order_id) DO NOTHING
             """, cancellationToken);

    public async Task<Guid?> ResolveUserIdByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<Guid>(
            $"""SELECT user_id AS "Value" FROM order_user_index WHERE order_id = {orderId}""").ToListAsync(cancellationToken);

        return rows.Count > 0 ? rows[0] : null;
    }

    public Task SeedTrackingOrderIndexAsync(string trackingId, Guid orderId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO tracking_order_index (tracking_id, order_id) VALUES ({trackingId}, {orderId})
             ON CONFLICT (tracking_id) DO NOTHING
             """, cancellationToken);

    public async Task<Guid?> ResolveUserIdByTrackingIdAsync(string trackingId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<Guid>(
            $"""
             SELECT oui.user_id AS "Value"
             FROM tracking_order_index toi
             JOIN order_user_index oui ON oui.order_id = toi.order_id
             WHERE toi.tracking_id = {trackingId}
             """).ToListAsync(cancellationToken);

        return rows.Count > 0 ? rows[0] : null;
    }
}
