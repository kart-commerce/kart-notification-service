using Kart.Notification.Application.Features.ProcessNotificationTrigger;
using Kart.Notification.Domain.Catalog;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>NOTIF-8's `UserRegistered` (Tier 2 welcome notification) - carries `userId` directly, no index lookup needed.</summary>
public static class IdentityEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var payload = DispatcherSupport.Deserialize<UserRegisteredPayload>(body);
        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(
            new ProcessNotificationTriggerCommand(payload.EventId, payload.UserId, TriggeringEventType.UserRegistered, retryCount), cancellationToken);
        return result.ToOutcome();
    }
}
