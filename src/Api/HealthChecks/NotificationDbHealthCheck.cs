using Kart.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kart.Notification.Api.HealthChecks;

/// <summary>
/// Readiness signal for `/health/ready` - a database that is reachable but behind on migrations
/// (e.g. `notification_attempts` never partitioned/created) must fail readiness too, not just an
/// unreachable one, so a pod never accepts traffic (starts consuming) while its schema isn't ready.
/// </summary>
public sealed class NotificationDbHealthCheck(NotificationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            return pending.Length == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending.Length} pending migration(s): {string.Join(", ", pending)}");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Notification database is unreachable", exception);
        }
    }
}
