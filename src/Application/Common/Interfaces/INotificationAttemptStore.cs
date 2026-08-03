using Kart.Notification.Domain.Enums;

namespace Kart.Notification.Application.Common.Interfaces;

/// <summary>
/// database-design.md's Write Mechanics steps 1/2, implemented as raw parameterized SQL in
/// `Infrastructure.Persistence.NotificationAttemptStore` — the `(event_id, channel)` unique
/// constraint plus insert-first/`ON CONFLICT` semantics is the idempotency mechanism itself
/// (ddd-model.md Modeling Decision #2), not a pre-check this interface performs in application code.
/// </summary>
public interface INotificationAttemptStore
{
    /// <summary>
    /// Step 1's combined opt-out-check + idempotency-insert CTE. Returns <c>null</c> when 0 rows
    /// were returned (this `(eventId, channel)` already exists — a redelivery no-op: no delivery
    /// is attempted and `NotificationSent` is not republished). Otherwise returns the row's
    /// freshly-inserted status: <see cref="DeliveryOutcome.Pending"/> or <see cref="DeliveryOutcome.Suppressed"/>.
    /// </summary>
    Task<DeliveryOutcome?> TryCreateAsync(
        Guid eventId,
        Channel channel,
        Guid userId,
        string triggeringEventType,
        CriticalityTier tier,
        string category,
        CancellationToken cancellationToken);

    /// <summary>Step 2's success path: `status = Sent`, `attempt_count` incremented.</summary>
    Task MarkSentAsync(Guid eventId, Channel channel, CancellationToken cancellationToken);

    /// <summary>Step 2's failure-but-budget-remaining path: `attempt_count` incremented only, `status` stays `Pending`. Returns the new attempt count.</summary>
    Task<int> IncrementAttemptAsync(Guid eventId, Channel channel, CancellationToken cancellationToken);

    /// <summary>Step 2's failure-and-budget-exhausted path: `status = Failed`, `attempt_count` incremented.</summary>
    Task MarkFailedAsync(Guid eventId, Channel channel, CancellationToken cancellationToken);
}
