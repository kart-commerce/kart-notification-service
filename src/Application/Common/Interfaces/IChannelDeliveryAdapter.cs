using Kart.Notification.Domain.Enums;

namespace Kart.Notification.Application.Common.Interfaces;

/// <summary>Everything a channel adapter needs to attempt one physical send — deliberately thin, no PII beyond the opaque `userId` reference (database-design.md's PII classification: delivery addresses are resolved inside the adapter's own lookup, never persisted to this schema).</summary>
public sealed record NotificationDeliveryContext(Guid EventId, Guid UserId, string TriggeringEventType, string Category);

public enum ChannelDeliveryStatus
{
    Sent,
    Failed,

    /// <summary>The circuit breaker for this channel's downstream provider is open — treated the same as <see cref="Failed"/> by the caller (counts against the attempt budget), but logged/metriced distinctly.</summary>
    CircuitOpen,
}

public sealed record ChannelDeliveryResult(ChannelDeliveryStatus Status, string? FailureReason = null)
{
    public static readonly ChannelDeliveryResult Sent = new(ChannelDeliveryStatus.Sent);
}

/// <summary>
/// NOTIF-10: one implementation per <see cref="Channel"/>, each wrapped in its own circuit breaker
/// (design-decisions.md's Resilience Pattern decision) so one provider's outage never blocks
/// another channel's capacity. No third-party provider is named anywhere in the approved
/// requirements - these are provider-agnostic interfaces with a working simulated implementation,
/// ready to swap for a real SendGrid/Twilio/FCM client (the same "operator-configured, empty by
/// default" treatment kart-identity-service applies to EnterpriseIdps/SocialIdps).
/// </summary>
public interface IChannelDeliveryAdapter
{
    Channel Channel { get; }

    Task<ChannelDeliveryResult> SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken);
}

public interface IChannelDeliveryAdapterFactory
{
    IChannelDeliveryAdapter Resolve(Channel channel);
}
