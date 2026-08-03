using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>NOTIF-7's `DeliveryStatusUpdated` leg - the one triggering event carrying neither `userId` nor `orderId`, resolved via the full two-hop chain `tracking_order_index -> order_user_index` (ADR-0020).</summary>
public static class TrackingEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var payload = DispatcherSupport.Deserialize<DeliveryStatusUpdatedPayload>(body);
        var resolver = sp.GetRequiredService<IUserIdResolutionService>();

        var userId = await resolver.ResolveUserIdByTrackingIdAsync(payload.TrackingId, cancellationToken);
        if (userId is null)
        {
            return NotificationDispatchOutcome.RetryTransient;
        }

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(
            new ProcessNotificationTriggerCommand(payload.EventId, userId.Value, TriggeringEventType.DeliveryStatusUpdated, retryCount), cancellationToken);
        return result.ToOutcome();
    }
}
