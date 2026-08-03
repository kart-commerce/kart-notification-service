namespace Kart.Notification.Domain.Enums;

/// <summary>
/// ddd-model.md's <c>DeliveryOutcome</c>. `Pending -> {Sent, Failed, Suppressed}` only, and only
/// once — enforced at the DB layer by `trg_notification_attempts_status_guard`, not just in code.
/// </summary>
public enum DeliveryOutcome
{
    Pending,
    Sent,
    Failed,
    Suppressed,
}

public static class DeliveryOutcomeExtensions
{
    public static bool IsTerminal(this DeliveryOutcome outcome) =>
        outcome is DeliveryOutcome.Sent or DeliveryOutcome.Failed or DeliveryOutcome.Suppressed;
}
