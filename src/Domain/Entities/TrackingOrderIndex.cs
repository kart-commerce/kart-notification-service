namespace Kart.Notification.Domain.Entities;

/// <summary>
/// ADR-0020's `TrackingOrderIndex` lookup projection — `trackingId -> orderId`, seeded solely from
/// `ShipmentDispatched`. Chains `DeliveryStatusUpdated`'s `trackingId` to <see cref="OrderUserIndex"/>'s
/// `orderId`. Not an aggregate root.
/// </summary>
public sealed class TrackingOrderIndex
{
    public string TrackingId { get; private set; } = string.Empty;

    public Guid OrderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    private TrackingOrderIndex()
    {
    }
}
