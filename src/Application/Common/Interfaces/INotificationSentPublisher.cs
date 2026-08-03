using Kart.Notification.Domain.Enums;

namespace Kart.Notification.Application.Common.Interfaces;

/// <summary>
/// NOTIF-12: publishes `NotificationSent` directly, best-effort, immediately after a
/// `NotificationAttempt` row reaches a terminal <see cref="DeliveryOutcome"/> — no Transactional
/// Outbox (database-design.md's "No Transactional Outbox" section: this publish's own stated
/// tier is 1x fire-and-forget, so no atomicity guarantee is required here).
/// </summary>
public interface INotificationSentPublisher
{
    Task PublishAsync(Guid userId, Channel channel, DeliveryOutcome status, CancellationToken cancellationToken);
}
