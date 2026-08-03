using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Kart.Notification.Infrastructure.ChannelAdapters;

/// <summary>No push provider (FCM/APNs/etc.) is named anywhere in the approved requirements - see <see cref="EmailChannelDeliveryAdapter"/>'s remarks.</summary>
public sealed class PushChannelDeliveryAdapter(ILogger<PushChannelDeliveryAdapter> logger)
    : ResilientChannelDeliveryAdapterBase(bulkheadCapacity: 100, logger)
{
    public override Channel Channel => Channel.Push;

    protected override Task<bool> TrySendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[simulated] Push sent for eventId={EventId} userId={UserId} triggeringEventType={TriggeringEventType}",
            context.EventId, context.UserId, context.TriggeringEventType);
        return Task.FromResult(true);
    }
}
