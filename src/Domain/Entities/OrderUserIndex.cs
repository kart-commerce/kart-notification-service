namespace Kart.Notification.Domain.Entities;

/// <summary>
/// ADR-0020's `OrderUserIndex` lookup projection — `orderId -> userId`, seeded solely from
/// `OrderCreated`. Not an aggregate root (no invariants beyond upsert-on-consume).
/// </summary>
public sealed class OrderUserIndex
{
    public Guid OrderId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    private OrderUserIndex()
    {
    }
}
