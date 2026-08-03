namespace Kart.Notification.Domain;

/// <summary>
/// BRD §24.3's audit-actor invariant: every write to this service's tables is made by one of
/// these well-known `system:*` principals — never `NULL`, since no human/API caller ever writes
/// any table here (ddd-model.md's audit-actor invariants for each aggregate/projection).
/// </summary>
public static class SystemPrincipals
{
    public const string NotificationSendPipeline = "system:notification-send-pipeline";
    public const string OrderUserIndexConsumer = "system:notification-order-user-index-consumer";
    public const string TrackingOrderIndexConsumer = "system:notification-tracking-order-index-consumer";
    public const string PreferenceSyncConsumer = "system:notification-preference-sync-consumer";
}
