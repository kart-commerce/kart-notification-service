namespace Kart.Notification.Infrastructure.Auditing;

/// <summary>
/// The concrete audit trail this service registers (`EfAuditLogWriter`) instead of
/// `Kart.Shared.Auditing`'s `NullAuditLogWriter` default - the user's own explicit ask for audit
/// logging goes beyond the platform's minimal per-service default (mirrors kart-order-service's
/// own precedent as the first service to register a real writer). Independent best-effort write
/// (its own `SaveChangesAsync`), not folded into the caller's transaction.
/// </summary>
public sealed class NotificationAuditLogEntry
{
    public Guid Id { get; private set; }

    public string ServiceName { get; private set; } = string.Empty;

    public string ActorId { get; private set; } = string.Empty;

    public string ActorType { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public string? MetadataJson { get; private set; }

    private NotificationAuditLogEntry()
    {
    }

    public static NotificationAuditLogEntry Create(
        string serviceName, string actorId, string actorType, string action, string entityType, string entityId, DateTimeOffset occurredAt, string? metadataJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            ServiceName = serviceName,
            ActorId = actorId,
            ActorType = actorType,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OccurredAt = occurredAt,
            MetadataJson = metadataJson,
        };
}
