using Kart.Notification.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kart.Notification.Infrastructure.Persistence;

/// <summary>database-design.md's Write Mechanics step 3 - a full-map-replace upsert, raw SQL for the same reasons as <see cref="NotificationAttemptStore"/>.</summary>
public sealed class NotificationPreferenceStore(NotificationDbContext dbContext) : INotificationPreferenceStore
{
    public Task UpsertAsync(Guid userId, string optOutMatrixJson, bool appInstalled, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO notification_preferences (user_id, opt_out_matrix, app_installed, last_updated_at)
             VALUES ({userId}, {optOutMatrixJson}::jsonb, {appInstalled}, now())
             ON CONFLICT (user_id) DO UPDATE
             SET opt_out_matrix = EXCLUDED.opt_out_matrix,
                 app_installed = EXCLUDED.app_installed,
                 last_updated_at = EXCLUDED.last_updated_at,
                 updated_by = 'system:notification-preference-sync-consumer'
             """, cancellationToken);

    public async Task<bool> GetAppInstalledAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database.SqlQuery<bool>(
            $"""SELECT app_installed AS "Value" FROM notification_preferences WHERE user_id = {userId}""").ToListAsync(cancellationToken);

        return rows.Count > 0 && rows[0];
    }
}
