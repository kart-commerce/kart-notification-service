namespace Kart.Notification.Infrastructure.Messaging;

// Minimal wire shapes for each of the 13 consumed events (event-contract.md's "Key Fields"
// columns) - only the fields this service's own pipeline actually needs (userId resolution +
// ProcessNotificationTriggerCommand's own shape), not a full mirror of each publisher's schema.

public sealed record OrderCreatedPayload(Guid EventId, Guid OrderId, Guid UserId);

public sealed record OrderLifecyclePayload(Guid EventId, Guid OrderId);

public sealed record PaymentEventPayload(Guid EventId, Guid OrderId);

public sealed record ShipmentDispatchedPayload(Guid EventId, Guid OrderId, string TrackingId);

public sealed record DeliveryStatusUpdatedPayload(Guid EventId, string TrackingId);

public sealed record UserRegisteredPayload(Guid EventId, Guid UserId);

public sealed record WishlistPriceAlertTriggeredPayload(Guid EventId, Guid UserId);

/// <summary>
/// `OptOutMatrix` here is assumed to already be the `{channel -> {category -> optedOut: bool}}`
/// shape database-design.md/ddd-model.md store verbatim (a full-map replace, Modeling Decision #7)
/// - the source docs use "opt-out map" and "notificationOptIn" inconsistently for this same event
/// payload; this service's own storage shape is the opt-out (not opt-in) convention, so that is
/// the shape assumed on the wire too.
/// </summary>
public sealed record UserNotificationPreferenceUpdatedPayload(Guid EventId, Guid UserId, Dictionary<string, Dictionary<string, bool>> OptOutMatrix, bool AppInstalled);
