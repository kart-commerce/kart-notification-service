using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kart.Notification.Infrastructure.Persistence;

/// <summary>
/// Raw parameterized SQL implementing database-design.md's Write Mechanics steps 1/2 literally -
/// EF Core's own change tracking is deliberately not used for this path so the DB's own
/// `INSERT ... ON CONFLICT` / trigger semantics are exactly what runs (ddd-model.md Modeling
/// Decision #2: the unique constraint is the idempotency mechanism, not a select-then-insert
/// pre-check the application layer performs).
/// </summary>
public sealed class NotificationAttemptStore(NotificationDbContext dbContext) : INotificationAttemptStore
{
    public async Task<DeliveryOutcome?> TryCreateAsync(
        Guid eventId,
        Channel channel,
        Guid userId,
        string triggeringEventType,
        CriticalityTier tier,
        string category,
        CancellationToken cancellationToken)
    {
        var channelValue = channel.ToDbValue();
        var tierValue = tier.ToString();

        var rows = await dbContext.Database.SqlQuery<string>(
            $"""
             WITH pref AS (
                 SELECT opt_out_matrix
                 FROM notification_preferences
                 WHERE user_id = {userId}
             )
             INSERT INTO notification_attempts
                 (event_id, channel, user_id, triggering_event_type, criticality_tier, category, status, suppressed_reason)
             SELECT
                 {eventId}, {channelValue}, {userId}, {triggeringEventType}, {tierValue}, {category},
                 CASE WHEN opted_out THEN 'Suppressed' ELSE 'Pending' END,
                 CASE WHEN opted_out THEN format('opted out of %s/%s', {channelValue}, {category}) ELSE NULL END
             FROM (
                 SELECT COALESCE(
                     (SELECT (pref.opt_out_matrix -> {channelValue} ->> {category})::boolean FROM pref),
                     false
                 ) AS opted_out
             ) resolved
             ON CONFLICT (event_id, channel) DO NOTHING
             RETURNING status AS "Value"
             """).ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : Enum.Parse<DeliveryOutcome>(rows[0]);
    }

    public Task MarkSentAsync(Guid eventId, Channel channel, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
             UPDATE notification_attempts
             SET status = 'Sent', attempt_count = attempt_count + 1
             WHERE event_id = {eventId} AND channel = {channel.ToDbValue()}
             """, cancellationToken);

    public async Task<int> IncrementAttemptAsync(Guid eventId, Channel channel, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<int>(
            $"""
             UPDATE notification_attempts
             SET attempt_count = attempt_count + 1
             WHERE event_id = {eventId} AND channel = {channel.ToDbValue()}
             RETURNING attempt_count AS "Value"
             """).ToListAsync(cancellationToken);

        return rows.Count == 0
            ? throw new InvalidOperationException($"No notification_attempts row found for ({eventId}, {channel}) to increment.")
            : rows[0];
    }

    public Task MarkFailedAsync(Guid eventId, Channel channel, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
             UPDATE notification_attempts
             SET status = 'Failed', attempt_count = attempt_count + 1
             WHERE event_id = {eventId} AND channel = {channel.ToDbValue()}
             """, cancellationToken);
}
