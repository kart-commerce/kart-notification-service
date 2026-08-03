namespace Kart.Notification.Infrastructure.Messaging;

/// <summary>What a queue dispatcher decided to do with one inbound message, after running it through the Application pipeline.</summary>
public enum NotificationDispatchOutcome
{
    /// <summary>Success, a business no-op (redelivery of an already-handled row), or a terminal outcome already recorded (Sent/Suppressed) - ack, remove from the queue.</summary>
    Ack,

    /// <summary>At least one channel (or the userId lookup itself) still has retry budget left - requeue onto the next TTL-ladder tier.</summary>
    RetryTransient,

    /// <summary>Retry budget exhausted for every outstanding channel this pass - the domain-level `Failed` row and `NotificationSent(failed)` publish already happened; the raw message itself still dead-letters for ops DLQ-inspection visibility.</summary>
    DeadLetter,
}
