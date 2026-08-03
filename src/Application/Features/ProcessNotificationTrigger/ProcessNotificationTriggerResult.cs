namespace Kart.Notification.Application.Features.ProcessNotificationTrigger;

/// <summary>
/// Tells the calling consumer hosted service what to do with the raw inbound message. Retry takes
/// priority over dead-lettering: if any channel still has attempt budget left, the whole message
/// requeues onto the next retry tier (safe - the `(eventId, channel)` idempotency check makes
/// already-terminal channels a no-op on redelivery). Only once no channel needs another attempt,
/// and at least one channel was exhausted in *this* processing pass, does the message itself get
/// dead-lettered (for ops DLQ-inspection visibility) - the domain-level `Failed` row and
/// `NotificationSent(failed)` publish already happened regardless.
/// </summary>
public sealed record ProcessNotificationTriggerResult(bool NeedsRetry, bool AnyExhaustedThisPass);
