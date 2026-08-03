using Kart.Notification.Domain.Enums;

namespace Kart.Notification.Domain.Catalog;

/// <summary>
/// Resolves userId via which lookup projection (ADR-0020), if any.
/// </summary>
public enum UserIdResolution
{
    /// <summary>The event's own payload already carries `userId` directly.</summary>
    Direct,

    /// <summary>Resolve via `order_user_index` keyed on the event's own `orderId`.</summary>
    ViaOrderIndex,

    /// <summary>Resolve via the two-hop chain `tracking_order_index -> order_user_index`, keyed on the event's own `trackingId`.</summary>
    ViaTrackingChain,
}

/// <summary>
/// One entry per consumed triggering event type — the static config mapping ddd-model.md's
/// Modeling Decisions #3 (CriticalityTier), #4 (NotificationCategory), #6 (channel-selection) and
/// ADR-0020 (userId resolution path) all describe as "an engineering default this aggregate reads,
/// not a modeled business computation." Extending ADR-0003's scope to a future producer/event only
/// ever extends this one table (Modeling Decision #10).
/// </summary>
public sealed record TriggeringEventDefinition(
    string EventType,
    CriticalityTier Tier,
    string Category,
    bool IncludesSms,
    UserIdResolution UserIdResolution)
{
    /// <summary>
    /// Email always (universal reachability, requirement-spec §6 Q2); +SMS only for the events
    /// this catalog flags as time-critical/Tier-1-money-moving; Push is always a *candidate*,
    /// gated at runtime by the caller's own `NotificationPreference.AppInstalled` flag (not a
    /// static per-event property, so it is not modeled in this table).
    /// </summary>
    public IReadOnlyList<Channel> CandidateChannelsExcludingPush() =>
        IncludesSms ? [Channel.Email, Channel.Sms] : [Channel.Email];
}

public static class TriggeringEventCatalog
{
    private static readonly IReadOnlyDictionary<string, TriggeringEventDefinition> Entries = new Dictionary<string, TriggeringEventDefinition>
    {
        // Tier 1 — elevated per ADR-0020: OrderCreated also seeds order_user_index, the
        // resolution linchpin for 7 other events. Not itself "time-critical/money-moving" in the
        // urgency sense (it's a routine order-placed confirmation, elevated for indexing reasons
        // only) - engineering default: no SMS for this one, unlike the genuine Tier-1 money events.
        [TriggeringEventType.OrderCreated] = new(TriggeringEventType.OrderCreated, CriticalityTier.Tier1, NotificationCategory.OrderUpdates, IncludesSms: false, UserIdResolution.Direct),

        [TriggeringEventType.PaymentCompleted] = new(TriggeringEventType.PaymentCompleted, CriticalityTier.Tier1, NotificationCategory.Payment, IncludesSms: true, UserIdResolution.ViaOrderIndex),
        [TriggeringEventType.PaymentFailed] = new(TriggeringEventType.PaymentFailed, CriticalityTier.Tier1, NotificationCategory.Payment, IncludesSms: true, UserIdResolution.ViaOrderIndex),
        [TriggeringEventType.RefundIssued] = new(TriggeringEventType.RefundIssued, CriticalityTier.Tier1, NotificationCategory.Payment, IncludesSms: true, UserIdResolution.ViaOrderIndex),

        [TriggeringEventType.OrderConfirmed] = new(TriggeringEventType.OrderConfirmed, CriticalityTier.Tier2, NotificationCategory.OrderUpdates, IncludesSms: false, UserIdResolution.ViaOrderIndex),
        [TriggeringEventType.OrderCancelled] = new(TriggeringEventType.OrderCancelled, CriticalityTier.Tier2, NotificationCategory.OrderUpdates, IncludesSms: false, UserIdResolution.ViaOrderIndex),
        [TriggeringEventType.OrderCompensationTriggered] = new(TriggeringEventType.OrderCompensationTriggered, CriticalityTier.Tier2, NotificationCategory.OrderUpdates, IncludesSms: false, UserIdResolution.ViaOrderIndex),
        [TriggeringEventType.OrderDelivered] = new(TriggeringEventType.OrderDelivered, CriticalityTier.Tier2, NotificationCategory.OrderUpdates, IncludesSms: false, UserIdResolution.ViaOrderIndex),

        // Time-critical delivery events — SMS included per requirement-spec §6 Q2.
        [TriggeringEventType.ShipmentDispatched] = new(TriggeringEventType.ShipmentDispatched, CriticalityTier.Tier2, NotificationCategory.Shipping, IncludesSms: true, UserIdResolution.ViaOrderIndex),
        [TriggeringEventType.DeliveryStatusUpdated] = new(TriggeringEventType.DeliveryStatusUpdated, CriticalityTier.Tier2, NotificationCategory.Shipping, IncludesSms: true, UserIdResolution.ViaTrackingChain),

        [TriggeringEventType.UserRegistered] = new(TriggeringEventType.UserRegistered, CriticalityTier.Tier2, NotificationCategory.Account, IncludesSms: false, UserIdResolution.Direct),

        [TriggeringEventType.WishlistPriceAlertTriggered] = new(TriggeringEventType.WishlistPriceAlertTriggered, CriticalityTier.Tier3, NotificationCategory.Marketing, IncludesSms: false, UserIdResolution.Direct),
    };

    public static TriggeringEventDefinition Get(string eventType) =>
        Entries.TryGetValue(eventType, out var definition)
            ? definition
            : throw new KeyNotFoundException($"'{eventType}' is not a recognized NotificationAttempt triggering event type (ADR-0003's approved scope).");

    public static bool TryGet(string eventType, out TriggeringEventDefinition? definition) =>
        Entries.TryGetValue(eventType, out definition);
}
