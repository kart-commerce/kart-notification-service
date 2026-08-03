using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain;
using Kart.Shared.Auditing;
using MediatR;

namespace Kart.Notification.Application.Features.UpsertNotificationPreference;

public sealed class UpsertNotificationPreferenceCommandHandler(
    INotificationPreferenceStore preferenceStore,
    IAuditLogWriter auditLogWriter)
    : IRequestHandler<UpsertNotificationPreferenceCommand>
{
    public async Task Handle(UpsertNotificationPreferenceCommand request, CancellationToken cancellationToken)
    {
        await preferenceStore.UpsertAsync(request.UserId, request.OptOutMatrixJson, request.AppInstalled, cancellationToken);

        await auditLogWriter.WriteAsync(
            AuditLogEntry.Create(
                "kart-notification-service",
                SystemPrincipals.PreferenceSyncConsumer,
                "system",
                "notification-preference.updated",
                "NotificationPreference",
                request.UserId.ToString()),
            cancellationToken);
    }
}
