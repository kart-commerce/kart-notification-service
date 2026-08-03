using System.Text.Json;
using Kart.Notification.Application.Features.ProcessNotificationTrigger;

namespace Kart.Notification.Infrastructure.Messaging;

internal static class DispatcherSupport
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static T Deserialize<T>(ReadOnlyMemory<byte> body) =>
        JsonSerializer.Deserialize<T>(body.Span, SerializerOptions)
            ?? throw new InvalidOperationException($"{typeof(T).Name} payload deserialized to null.");

    public static NotificationDispatchOutcome ToOutcome(this ProcessNotificationTriggerResult result) => result switch
    {
        { NeedsRetry: true } => NotificationDispatchOutcome.RetryTransient,
        { AnyExhaustedThisPass: true } => NotificationDispatchOutcome.DeadLetter,
        _ => NotificationDispatchOutcome.Ack,
    };
}
