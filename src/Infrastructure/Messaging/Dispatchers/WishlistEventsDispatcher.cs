using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>NOTIF-9's `WishlistPriceAlertTriggered` leg (Tier 3) - carries `userId` directly.</summary>
public static class WishlistEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var payload = DispatcherSupport.Deserialize<WishlistPriceAlertTriggeredPayload>(body);
        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(
            new ProcessNotificationTriggerCommand(payload.EventId, payload.UserId, TriggeringEventType.WishlistPriceAlertTriggered, retryCount), cancellationToken);
        return result.ToOutcome();
    }
}
