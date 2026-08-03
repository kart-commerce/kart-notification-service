namespace Kart.Notification.Domain.Entities;

/// <summary>
/// database-design.md's `notification_preferences` / ddd-model.md's `NotificationPreference`
/// aggregate — the local, eventually-consistent opt-out/reachability projection populated solely
/// by consuming `UserNotificationPreferenceUpdated`. `OptOutMatrixJson` is the raw `{channel ->
/// {category -> optedOut}}` JSONB map (Modeling Decision #7: replaced wholesale, never patched).
/// As with <see cref="NotificationAttempt"/>, the actual write path is
/// `Infrastructure.Persistence.NotificationPreferenceStore`'s raw upsert SQL, not EF change
/// tracking — this class is the EF-mapped read shape.
/// </summary>
public sealed class NotificationPreference
{
    public Guid UserId { get; private set; }

    public string OptOutMatrixJson { get; private set; } = "{}";

    public bool AppInstalled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastUpdatedAt { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    private NotificationPreference()
    {
    }
}
