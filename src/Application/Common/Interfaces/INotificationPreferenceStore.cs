namespace Kart.Notification.Application.Common.Interfaces;

/// <summary>database-design.md's Write Mechanics step 3 and `NotificationPreference.AppInstalled` reads.</summary>
public interface INotificationPreferenceStore
{
    /// <summary>
    /// Step 3's full-map-replace upsert (Modeling Decision #7) — naturally idempotent under
    /// `UserNotificationPreferenceUpdated`'s at-least-once redelivery, no separate dedup ledger needed.
    /// </summary>
    Task UpsertAsync(Guid userId, string optOutMatrixJson, bool appInstalled, CancellationToken cancellationToken);

    /// <summary>
    /// Whether `Push` is a candidate channel for this user (requirement-spec §6 Q2). Absence of a
    /// row means `UserNotificationPreferenceUpdated` hasn't been consumed for this user yet —
    /// defaults to <c>false</c> (don't attempt Push to an unknown-reachability user). This is a
    /// distinct default from the opt-out invariant's own "absence defaults to opted-in"
    /// (Modeling Decision #8) - that decision is about *not silently suppressing*, not about
    /// whether Push is reachable at all.
    /// </summary>
    Task<bool> GetAppInstalledAsync(Guid userId, CancellationToken cancellationToken);
}
