namespace Kart.Notification.Domain.Enums;

/// <summary>
/// database-design.md's `channel` CHECK constraint is `IN ('Email', 'SMS', 'Push')` — SMS is
/// upper-cased there (matching event-contract.md's own wire vocabulary), which doesn't match
/// <see cref="Channel"/>'s PascalCase C# member `Sms`. Centralizing the mapping here means every
/// persistence/messaging call site converts through one place instead of hand-rolling the SMS
/// special case repeatedly.
/// </summary>
public static class ChannelExtensions
{
    public static string ToDbValue(this Channel channel) => channel switch
    {
        Channel.Email => "Email",
        Channel.Sms => "SMS",
        Channel.Push => "Push",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown Channel."),
    };

    public static Channel FromDbValue(string value) => value switch
    {
        "Email" => Channel.Email,
        "SMS" => Channel.Sms,
        "Push" => Channel.Push,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown channel database value."),
    };
}
