using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Kart.Notification.Infrastructure.ChannelAdapters;

/// <summary>No SMS provider (Twilio/SNS/etc.) is named anywhere in the approved requirements - see <see cref="EmailChannelDeliveryAdapter"/>'s remarks.</summary>
public sealed class SmsChannelDeliveryAdapter(ILogger<SmsChannelDeliveryAdapter> logger)
    : ResilientChannelDeliveryAdapterBase(bulkheadCapacity: 20, logger)
{
    public override Channel Channel => Channel.Sms;

    protected override Task<bool> TrySendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[simulated] SMS sent for eventId={EventId} userId={UserId} triggeringEventType={TriggeringEventType}",
            context.EventId, context.UserId, context.TriggeringEventType);
        return Task.FromResult(true);
    }
}
