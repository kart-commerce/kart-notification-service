using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kart.Notification.Infrastructure.Observability;

/// <summary>
/// `Kart.Shared.Observability`'s own README flags RabbitMQ publish/consume spans as a known gap
/// (not auto-instrumented) - this fills it for this service specifically, since Notification's
/// trace root is always the propagated context of the triggering event (design-decisions.md's
/// Observability decision: "never a fresh span"), not an inbound HTTP request. Also hosts this
/// service's own custom metrics - the per-channel send-failure-rate signal design-decisions.md
/// calls out as "the earliest signal of a downstream provider outage, ahead of the circuit
/// breaker even tripping."
/// </summary>
public static class NotificationTelemetry
{
    public const string ActivitySourceName = "Kart.Notification";
    public const string MeterName = "Kart.Notification";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> NotificationAttemptsCreated =
        Meter.CreateCounter<long>("notification.attempts.created", unit: "{attempt}", description: "NotificationAttempt rows created, tagged by channel/tier/outcome.");

    public static readonly Counter<long> ChannelSendFailures =
        Meter.CreateCounter<long>("notification.channel.send_failures", unit: "{failure}", description: "Physical delivery failures per channel - the earliest signal of a downstream provider outage.");

    public static readonly Counter<long> Tier1BudgetExhausted =
        Meter.CreateCounter<long>("notification.tier1.budget_exhausted", unit: "{event}", description: "Tier 1 (paged) notifications that exhausted their retry budget - feeds on-call alerting.");

    /// <summary>
    /// Starts a consumer span for one inbound message, linking to the W3C `traceparent` header if
    /// the publisher sent one (best-effort - many upstream services don't populate it yet).
    /// </summary>
    public static Activity? StartConsumeActivity(string queueName, string? traceParentHeader)
    {
        ActivityContext.TryParse(traceParentHeader, null, out var parentContext);
        return ActivitySource.StartActivity($"{queueName} consume", ActivityKind.Consumer, parentContext);
    }
}
