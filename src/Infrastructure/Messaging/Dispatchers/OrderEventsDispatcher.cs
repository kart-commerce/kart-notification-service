using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using Kart.Notification.Infrastructure.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>
/// NOTIF-5's `OrderCreated` leg (Tier 1, seeds `order_user_index`) and NOTIF-6's four Tier-2
/// order-lifecycle events, sharing `notification.order-events.queue` per the manifest's `order.*`
/// wildcard binding (event-contract.md).
/// </summary>
public static class OrderEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var sender = sp.GetRequiredService<ISender>();
        var resolver = sp.GetRequiredService<IUserIdResolutionService>();

        if (routingKey == "order.order.created")
        {
            var payload = DispatcherSupport.Deserialize<OrderCreatedPayload>(body);
            await resolver.SeedOrderUserIndexAsync(payload.OrderId, payload.UserId, cancellationToken);

            var result = await sender.Send(
                new ProcessNotificationTriggerCommand(payload.EventId, payload.UserId, TriggeringEventType.OrderCreated, retryCount), cancellationToken);
            return result.ToOutcome();
        }

        var eventType = routingKey switch
        {
            "order.order.confirmed" => TriggeringEventType.OrderConfirmed,
            "order.order.cancelled" => TriggeringEventType.OrderCancelled,
            "order.order.compensation-triggered" => TriggeringEventType.OrderCompensationTriggered,
            "order.order.delivered" => TriggeringEventType.OrderDelivered,
            _ => throw new InvalidOperationException($"Unrecognized routing key '{routingKey}' on notification.order-events.queue."),
        };

        var lifecyclePayload = DispatcherSupport.Deserialize<OrderLifecyclePayload>(body);
        var userId = await resolver.ResolveUserIdByOrderIdAsync(lifecyclePayload.OrderId, cancellationToken);
        if (userId is null)
        {
            // ADR-0020: the seeding OrderCreated event hasn't been consumed yet - transient, not
            // permanent. Requeue onto this event's own already-modeled retry ladder.
            return NotificationDispatchOutcome.RetryTransient;
        }

        var lifecycleResult = await sender.Send(
            new ProcessNotificationTriggerCommand(lifecyclePayload.EventId, userId.Value, eventType, retryCount), cancellationToken);
        return lifecycleResult.ToOutcome();
    }
}
