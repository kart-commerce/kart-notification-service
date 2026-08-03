using Kart.Notification.Domain.Enums;

namespace Kart.Notification.Domain.Entities;

/// <summary>
/// database-design.md's `notification_attempts` / ddd-model.md's `NotificationAttempt` aggregate.
/// The row's own insert (via `Infrastructure.Persistence.NotificationAttemptStore`'s
/// `INSERT ... ON CONFLICT (event_id, channel) DO NOTHING` CTE) *is* the idempotency mechanism —
/// this class exists for EF Core mapping/migrations and read-only projections (e.g. the future
/// per-user audit lookup `idx_notification_attempts_user_audit` supports), not as the primary
/// write path. Every actual mutation goes through the raw parameterized SQL in
/// `NotificationAttemptStore`, which mirrors database-design.md's Write Mechanics literally —
/// EF's own change tracking is deliberately not used for the (eventId, channel) insert/update
/// path, so the DB's own `ON CONFLICT`/trigger semantics are exactly what runs, not whatever SQL
/// EF Core's change tracker would generate for an equivalent LINQ operation.
/// </summary>
public sealed class NotificationAttempt
{
    public Guid EventId { get; private set; }

    public Channel Channel { get; private set; }

    public Guid UserId { get; private set; }

    public string TriggeringEventType { get; private set; } = string.Empty;

    public CriticalityTier CriticalityTier { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public DeliveryOutcome Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? SuppressedReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    private NotificationAttempt()
    {
    }
}
