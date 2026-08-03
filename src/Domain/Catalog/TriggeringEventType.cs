namespace Kart.Notification.Domain.Catalog;

/// <summary>
/// The 13 event type names ADR-0003/event-contract.md approve as consumed triggers, exactly as
/// they appear on the wire (`eventType` — PascalCase `&lt;Entity&gt;&lt;PastTenseVerb&gt;`, per
/// naming-conventions.md). `UserNotificationPreferenceUpdated` never creates a `NotificationAttempt`
/// (it only updates `NotificationPreference`) but is listed here too since it shares this same
/// vocabulary of "a triggering event type this service consumes."
/// </summary>
public static class TriggeringEventType
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderCancelled = "OrderCancelled";
    public const string OrderCompensationTriggered = "OrderCompensationTriggered";
    public const string OrderDelivered = "OrderDelivered";
    public const string PaymentCompleted = "PaymentCompleted";
    public const string PaymentFailed = "PaymentFailed";
    public const string RefundIssued = "RefundIssued";
    public const string ShipmentDispatched = "ShipmentDispatched";
    public const string DeliveryStatusUpdated = "DeliveryStatusUpdated";
    public const string UserRegistered = "UserRegistered";
    public const string WishlistPriceAlertTriggered = "WishlistPriceAlertTriggered";
    public const string UserNotificationPreferenceUpdated = "UserNotificationPreferenceUpdated";
}
