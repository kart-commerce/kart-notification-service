using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Kart.Notification.Infrastructure.ChannelAdapters;

/// <summary>
/// No email provider (SendGrid/SES/etc.) is named anywhere in the approved requirements - this is
/// a working simulated implementation (logs and reports success), ready to swap for a real client
/// behind the same <see cref="IChannelDeliveryAdapter"/> port. The delivery address itself would be
/// resolved here, against Identity/User profile data, at send time - never persisted to this
/// service's own schema (database-design.md's PII classification).
/// </summary>
public sealed class EmailChannelDeliveryAdapter(ILogger<EmailChannelDeliveryAdapter> logger)
    : ResilientChannelDeliveryAdapterBase(bulkheadCapacity: 50, logger)
{
    public override Channel Channel => Channel.Email;

    protected override Task<bool> TrySendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[simulated] Email sent for eventId={EventId} userId={UserId} triggeringEventType={TriggeringEventType}",
            context.EventId, context.UserId, context.TriggeringEventType);
        return Task.FromResult(true);
    }
}
