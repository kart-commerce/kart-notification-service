using System.Text;
using System.Text.Json;
using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;
using Kart.Notification.Infrastructure.Observability;
using Kart.Shared.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Kart.Notification.Infrastructure.Messaging;

/// <summary>
/// NOTIF-12: publishes `NotificationSent` directly onto `notification.exchange`, best-effort,
/// right after a `NotificationAttempt` row reaches a terminal status - no Transactional Outbox
/// (database-design.md's "No Transactional Outbox" section). A fresh, short-lived channel per
/// publish over a shared, reused connection avoids any cross-thread `IModel` sharing (RabbitMQ.Client's
/// channels aren't thread-safe) without needing a lock on a hot path - acceptable given this is a
/// 1x, fire-and-forget publish, not a high-frequency loop.
/// </summary>
public sealed class NotificationSentPublisher(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    ILogger<NotificationSentPublisher> logger)
    : INotificationSentPublisher, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Lazy<IConnection> _connection = new(connectionFactory.CreateConnection);

    public Task PublishAsync(Guid userId, Channel channel, DeliveryOutcome status, CancellationToken cancellationToken)
    {
        try
        {
            using var activity = NotificationTelemetry.ActivitySource.StartActivity("notification.exchange publish", System.Diagnostics.ActivityKind.Producer);

            using var model = _connection.Value.CreateModel();
            RabbitMqTopologyProvisioner.Declare(model, manifest);

            var payload = new { userId, channel = channel.ToDbValue(), status = status.ToString().ToLowerInvariant() };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, SerializerOptions));

            var exchange = manifest.ExchangeFor("NotificationSent");
            var routingKey = manifest.RoutingKeyFor("NotificationSent");

            var properties = model.CreateBasicProperties();
            properties.Persistent = false; // 1x, fire-and-forget audit-publish tier - no durability guarantee required.
            properties.ContentType = "application/json";

            model.BasicPublish(exchange, routingKey, properties, body);
        }
        catch (Exception ex)
        {
            // Best-effort by design (requirement-spec §6 Q6) - losing this publish only delays
            // Analytics' visibility into an already-resolved outcome, never the user-facing
            // delivery itself, which already happened (or was recorded as Failed/Suppressed)
            // before this call.
            logger.LogWarning(ex, "Failed to publish NotificationSent for userId={UserId} channel={Channel} status={Status} - best-effort, not retried.", userId, channel, status);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }
}
