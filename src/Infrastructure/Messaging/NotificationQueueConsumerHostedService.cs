using Kart.Notification.Infrastructure.Observability;
using Kart.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Kart.Notification.Infrastructure.Messaging;

public delegate Task<NotificationDispatchOutcome> NotificationQueueDispatcher(
    string routingKey, ReadOnlyMemory<byte> body, IServiceProvider scopedProvider, int retryCount, CancellationToken cancellationToken);

/// <summary>
/// One shared mechanism for all 7 of this service's consumer queues (mirrors ddd-model.md
/// Modeling Decision #10's "one bounded context, one path" philosophy, applied to the consumer
/// side too) - reconnect loop, manual ack/nack, and TTL-ladder retry routing, identical for every
/// queue. This deliberately does <b>not</b> reuse `Kart.Shared.Messaging`'s
/// `RabbitMqConsumerHostedServiceBase`: that base class gates retry-vs-dead-letter purely on the
/// queue's own ladder rung count, but `notification.order-events.queue` and
/// `notification.payment-events.queue` each carry triggering events from more than one
/// criticality tier (wildcard `order.*`/`payment.*` bindings) - the retry ceiling here must be
/// enforced per event type (via `TriggeringEventCatalog`, inside the dispatcher), not per queue.
/// Each queue's own <see cref="NotificationQueueDispatcher"/> owns that per-event decision and
/// returns a <see cref="NotificationDispatchOutcome"/>; this class only executes it (ack, requeue
/// to the next ladder tier, or reject to the DLX).
/// </summary>
public sealed class NotificationQueueConsumerHostedService(
    IConnectionFactory connectionFactory,
    MessageBusManifest manifest,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationQueueConsumerHostedService> logger,
    string queueName,
    string retryCountHeaderName,
    NotificationQueueDispatcher dispatch)
    : BackgroundService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologyProvisioner.Declare(channel, manifest);
                channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += (_, delivery) => OnMessageReceivedAsync(channel, delivery, stoppingToken);
                channel.BasicConsume(queueName, autoAck: false, consumer);

                await WaitWhileConnectedAsync(connection, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Queue} consumer lost its RabbitMQ connection; reconnecting in {Delay}.", queueName, ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private static Task WaitWhileConnectedAsync(IConnection connection, CancellationToken stoppingToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdown += (_, _) => tcs.TrySetResult();
        using var registration = stoppingToken.Register(() => tcs.TrySetCanceled(stoppingToken));
        return tcs.Task;
    }

    private async Task OnMessageReceivedAsync(IModel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var retryCount = GetRetryCount(delivery.BasicProperties);
        var traceParent = delivery.BasicProperties.Headers?.TryGetValue("traceparent", out var raw) == true
            ? System.Text.Encoding.UTF8.GetString((byte[])raw)
            : null;

        using var activity = NotificationTelemetry.StartConsumeActivity(queueName, traceParent);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var outcome = await dispatch(delivery.RoutingKey, delivery.Body, scope.ServiceProvider, retryCount, stoppingToken);

            switch (outcome)
            {
                case NotificationDispatchOutcome.Ack:
                    channel.BasicAck(delivery.DeliveryTag, multiple: false);
                    break;
                case NotificationDispatchOutcome.RetryTransient:
                    RouteToNextRetryTier(channel, delivery, retryCount);
                    break;
                case NotificationDispatchOutcome.DeadLetter:
                    logger.LogCritical(
                        "{Queue} message permanently failed (retry budget exhausted): routingKey={RoutingKey}, deliveryTag={DeliveryTag}. Requires on-call attention.",
                        queueName, delivery.RoutingKey, delivery.DeliveryTag);
                    channel.BasicReject(delivery.DeliveryTag, requeue: false);
                    break;
            }
        }
        catch (Exception ex)
        {
            // An unexpected (bug/infra) exception, not a modeled business outcome - fall back to
            // the queue's own raw ladder-rung-count ceiling (the same default
            // RabbitMqConsumerHostedServiceBase applies), since we can't know the specific event's
            // own tier ceiling if dispatch itself failed before determining it.
            logger.LogError(ex, "Unhandled exception processing {Queue} message (delivery tag {DeliveryTag}).", queueName, delivery.DeliveryTag);
            var tiers = manifest.GetQueue(queueName).RetryLadder?.Tiers ?? [];
            if (retryCount < tiers.Count)
            {
                RouteToNextRetryTier(channel, delivery, retryCount);
            }
            else
            {
                channel.BasicReject(delivery.DeliveryTag, requeue: false);
            }
        }
    }

    private void RouteToNextRetryTier(IModel channel, BasicDeliverEventArgs delivery, int retryCount)
    {
        var tiers = manifest.GetQueue(queueName).RetryLadder?.Tiers ?? [];
        if (retryCount >= tiers.Count)
        {
            // The dispatcher asked to retry, but this queue's own ladder has no more rungs left
            // (should not happen given ladders are sized to each queue's deepest tier - defensive
            // fallback, dead-letter rather than silently dropping).
            logger.LogCritical("{Queue} requested a retry beyond its own ladder depth ({TierCount} rungs) for delivery tag {DeliveryTag}; dead-lettering.", queueName, tiers.Count, delivery.DeliveryTag);
            channel.BasicReject(delivery.DeliveryTag, requeue: false);
            return;
        }

        var retryQueueName = tiers[retryCount].Name;
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = delivery.BasicProperties.ContentType;
        properties.MessageId = delivery.BasicProperties.MessageId;
        properties.Headers = new Dictionary<string, object> { [retryCountHeaderName] = retryCount + 1 };

        channel.BasicPublish(exchange: string.Empty, routingKey: retryQueueName, basicProperties: properties, body: delivery.Body);
        channel.BasicAck(delivery.DeliveryTag, multiple: false);
    }

    private int GetRetryCount(IBasicProperties properties)
    {
        if (properties.Headers is not null && properties.Headers.TryGetValue(retryCountHeaderName, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes => int.Parse(System.Text.Encoding.UTF8.GetString(bytes)),
                _ => 0,
            };
        }

        return 0;
    }
}
