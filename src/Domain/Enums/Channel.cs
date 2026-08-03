namespace Kart.Notification.Domain.Enums;

/// <summary>ddd-model.md's <c>Channel</c> value object. String-serialized everywhere (DB `CHECK`, JSON) — never the numeric ordinal.</summary>
public enum Channel
{
    Email,
    Sms,
    Push,
}
