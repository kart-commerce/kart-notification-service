namespace Kart.Notification.Domain.Enums;

/// <summary>
/// ddd-model.md's <c>CriticalityTier</c> — snapshotted onto a <c>NotificationAttempt</c> at
/// creation time (Modeling Decision #3), never re-derived mid-flight. Each tier's <see cref="MaxAttempts"/>
/// is the retry-ladder ceiling design-decisions.md fixes: Tier1 5 (+paged), Tier2 3, Tier3 2.
/// </summary>
public enum CriticalityTier
{
    Tier1,
    Tier2,
    Tier3,
}

public static class CriticalityTierExtensions
{
    public static int MaxAttempts(this CriticalityTier tier) => tier switch
    {
        CriticalityTier.Tier1 => 5,
        CriticalityTier.Tier2 => 3,
        CriticalityTier.Tier3 => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown CriticalityTier."),
    };

    public static bool IsPaged(this CriticalityTier tier) => tier == CriticalityTier.Tier1;
}
