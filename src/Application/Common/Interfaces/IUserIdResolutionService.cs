namespace Kart.Notification.Application.Common.Interfaces;

/// <summary>
/// NOTIF-4 / ADR-0020: seeds and reads the two lookup projections that resolve `userId` for the
/// nine of thirteen triggering events whose own payload does not carry it. A <c>null</c> resolve
/// result means the seeding event hasn't been consumed yet — callers must treat this as transient
/// (requeue onto the dependent event's own retry ladder), never a permanent failure
/// (ddd-model.md's Lookup Projections section).
/// </summary>
public interface IUserIdResolutionService
{
    /// <summary>Seed on consuming `OrderCreated` (`order_user_index`, upsert, `ON CONFLICT (order_id) DO NOTHING`).</summary>
    Task SeedOrderUserIndexAsync(Guid orderId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Resolve `userId` for the eight `orderId`-keyed triggering events.</summary>
    Task<Guid?> ResolveUserIdByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Seed on consuming `ShipmentDispatched` (`tracking_order_index`, upsert, `ON CONFLICT (tracking_id) DO NOTHING`).</summary>
    Task SeedTrackingOrderIndexAsync(string trackingId, Guid orderId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolve `userId` for `DeliveryStatusUpdated` via the two-hop chain
    /// `tracking_order_index -> order_user_index`.
    /// </summary>
    Task<Guid?> ResolveUserIdByTrackingIdAsync(string trackingId, CancellationToken cancellationToken);
}
