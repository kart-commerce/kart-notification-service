using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>NOTIF-5's three Tier-1 payment events, sharing `notification.payment-events.queue` per the manifest's `payment.*` wildcard binding.</summary>
public static class PaymentEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var eventType = routingKey switch
        {
            "payment.intent.completed" => TriggeringEventType.PaymentCompleted,
            "payment.intent.failed" => TriggeringEventType.PaymentFailed,
            "payment.refund.issued" => TriggeringEventType.RefundIssued,
            _ => throw new InvalidOperationException($"Unrecognized routing key '{routingKey}' on notification.payment-events.queue."),
        };

        var payload = DispatcherSupport.Deserialize<PaymentEventPayload>(body);
        var resolver = sp.GetRequiredService<IUserIdResolutionService>();
        var userId = await resolver.ResolveUserIdByOrderIdAsync(payload.OrderId, cancellationToken);
        if (userId is null)
        {
            return NotificationDispatchOutcome.RetryTransient;
        }

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(new ProcessNotificationTriggerCommand(payload.EventId, userId.Value, eventType, retryCount), cancellationToken);
        return result.ToOutcome();
    }
}
