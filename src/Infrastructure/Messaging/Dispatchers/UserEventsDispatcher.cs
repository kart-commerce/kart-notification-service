using System.Text.Json;
using Kart.Notification.Application.Features.UpsertNotificationPreference;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Notification.Infrastructure.Messaging.Dispatchers;

/// <summary>NOTIF-9's `UserNotificationPreferenceUpdated` leg - a full-map-replace upsert, naturally idempotent under redelivery, so this always acks (no per-attempt retry ceiling to track, unlike `NotificationAttempt`).</summary>
public static class UserEventsDispatcher
{
    public static async Task<NotificationDispatchOutcome> DispatchAsync(
        string routingKey, ReadOnlyMemory<byte> body, IServiceProvider sp, int retryCount, CancellationToken cancellationToken)
    {
        var payload = DispatcherSupport.Deserialize<UserNotificationPreferenceUpdatedPayload>(body);
        var optOutMatrixJson = JsonSerializer.Serialize(payload.OptOutMatrix, DispatcherSupport.SerializerOptions);

        var sender = sp.GetRequiredService<ISender>();
        await sender.Send(new UpsertNotificationPreferenceCommand(payload.UserId, optOutMatrixJson, payload.AppInstalled), cancellationToken);

        return NotificationDispatchOutcome.Ack;
    }
}
