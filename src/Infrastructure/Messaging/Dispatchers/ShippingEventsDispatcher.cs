using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>NOTIF-7's `ShipmentDispatched` leg - resolves `userId` via `order_user_index` and seeds `tracking_order_index` for the later `DeliveryStatusUpdated` two-hop chain.</summary>
public static class ShippingEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var payload = DispatcherSupport.Deserialize<ShipmentDispatchedPayload>(body);
        var resolver = sp.GetRequiredService<IUserIdResolutionService>();

        var userId = await resolver.ResolveUserIdByOrderIdAsync(payload.OrderId, cancellationToken);
        if (userId is null)
        {
            return NotificationDispatchOutcome.RetryTransient;
        }

        await resolver.SeedTrackingOrderIndexAsync(payload.TrackingId, payload.OrderId, cancellationToken);

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(
            new ProcessNotificationTriggerCommand(payload.EventId, userId.Value, TriggeringEventType.ShipmentDispatched, retryCount), cancellationToken);
        return result.ToOutcome();
    }
}
