using MediatR;

namespace Kart.Notification.Application.Features.UpsertNotificationPreference;

/// <summary>
/// NOTIF-9's `UserNotificationPreferenceUpdated` leg - database-design.md's Write Mechanics step 3,
/// a full-map-replace upsert (ddd-model.md Modeling Decision #7), naturally idempotent under
/// at-least-once redelivery (Modeling Decision #9 - no separate dedup ledger needed here, unlike
/// <c>NotificationAttempt</c>).
/// </summary>
public sealed record UpsertNotificationPreferenceCommand(
    Guid UserId,
    string OptOutMatrixJson,
    bool AppInstalled) : IRequest;
