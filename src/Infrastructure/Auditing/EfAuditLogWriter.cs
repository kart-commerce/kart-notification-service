using System.Text.Json;
using Kart.Notification.Infrastructure.Persistence;
using Kart.Shared.Auditing;

namespace Kart.Notification.Infrastructure.Auditing;

/// <summary>The concrete `IAuditLogWriter` this service registers instead of the shared package's `NullAuditLogWriter` default.</summary>
public sealed class EfAuditLogWriter(NotificationDbContext dbContext) : IAuditLogWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        var metadataJson = entry.Metadata is null ? null : JsonSerializer.Serialize(entry.Metadata, SerializerOptions);

        dbContext.AuditLogEntries.Add(NotificationAuditLogEntry.Create(
            entry.ServiceName, entry.ActorId, entry.ActorType, entry.Action, entry.EntityType, entry.EntityId, entry.OccurredAt, metadataJson));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
