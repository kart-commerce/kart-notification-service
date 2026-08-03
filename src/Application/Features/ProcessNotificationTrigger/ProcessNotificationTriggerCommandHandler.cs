using Kart.Notification.Application.Common.Interfaces;
using Kart.Notification.Domain;
using Kart.Notification.Domain.Catalog;
using Kart.Notification.Domain.Enums;
using Kart.Shared.Auditing;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kart.Notification.Application.Features.ProcessNotificationTrigger;

public sealed class ProcessNotificationTriggerCommandHandler(
    INotificationAttemptStore attemptStore,
    INotificationPreferenceStore preferenceStore,
    IChannelDeliveryAdapterFactory adapterFactory,
    INotificationSentPublisher publisher,
    IAuditLogWriter auditLogWriter,
    ILogger<ProcessNotificationTriggerCommandHandler> logger)
    : IRequestHandler<ProcessNotificationTriggerCommand, ProcessNotificationTriggerResult>
{
    public async Task<ProcessNotificationTriggerResult> Handle(ProcessNotificationTriggerCommand request, CancellationToken cancellationToken)
    {
        var definition = TriggeringEventCatalog.Get(request.TriggeringEventType);

        var appInstalled = await preferenceStore.GetAppInstalledAsync(request.UserId, cancellationToken);
        var channels = definition.CandidateChannelsExcludingPush().ToList();
        if (appInstalled)
        {
            channels.Add(Channel.Push);
        }

        var needsRetry = false;
        var anyExhausted = false;

        foreach (var channel in channels)
        {
            var outcome = await attemptStore.TryCreateAsync(
                request.EventId, channel, request.UserId, request.TriggeringEventType, definition.Tier, definition.Category, cancellationToken);

            if (outcome is null)
            {
                // Redelivery of an already-handled (eventId, channel) - full no-op (ddd-model.md's
                // uniqueness invariant). Not counted as needing retry or as exhausted.
                continue;
            }

            if (outcome == DeliveryOutcome.Suppressed)
            {
                await publisher.PublishAsync(request.UserId, channel, DeliveryOutcome.Suppressed, cancellationToken);
                await WriteAuditAsync(request, channel, "notification.suppressed", cancellationToken);
                continue;
            }

            // outcome == Pending: attempt physical delivery.
            var adapter = adapterFactory.Resolve(channel);
            var deliveryContext = new NotificationDeliveryContext(request.EventId, request.UserId, request.TriggeringEventType, definition.Category);
            var deliveryResult = await adapter.SendAsync(deliveryContext, cancellationToken);

            if (deliveryResult.Status == ChannelDeliveryStatus.Sent)
            {
                await attemptStore.MarkSentAsync(request.EventId, channel, cancellationToken);
                await publisher.PublishAsync(request.UserId, channel, DeliveryOutcome.Sent, cancellationToken);
                await WriteAuditAsync(request, channel, "notification.sent", cancellationToken);
                continue;
            }

            var newAttemptCount = await attemptStore.IncrementAttemptAsync(request.EventId, channel, cancellationToken);
            var budgetExhausted = newAttemptCount >= definition.Tier.MaxAttempts();

            if (budgetExhausted)
            {
                await attemptStore.MarkFailedAsync(request.EventId, channel, cancellationToken);
                await publisher.PublishAsync(request.UserId, channel, DeliveryOutcome.Failed, cancellationToken);
                await WriteAuditAsync(request, channel, "notification.failed", cancellationToken);
                anyExhausted = true;

                if (definition.Tier.IsPaged())
                {
                    logger.LogCritical(
                        "Tier 1 notification permanently failed after exhausting its retry budget: eventId={EventId}, channel={Channel}, triggeringEventType={TriggeringEventType}. Requires on-call attention.",
                        request.EventId, channel, request.TriggeringEventType);
                }
            }
            else
            {
                needsRetry = true;
                logger.LogWarning(
                    "Notification delivery attempt {AttemptCount}/{MaxAttempts} failed: eventId={EventId}, channel={Channel}, reason={Reason}",
                    newAttemptCount, definition.Tier.MaxAttempts(), request.EventId, channel, deliveryResult.FailureReason);
            }
        }

        return new ProcessNotificationTriggerResult(needsRetry, anyExhausted);
    }

    private Task WriteAuditAsync(ProcessNotificationTriggerCommand request, Channel channel, string action, CancellationToken cancellationToken) =>
        auditLogWriter.WriteAsync(
            AuditLogEntry.Create(
                "kart-notification-service",
                SystemPrincipals.NotificationSendPipeline,
                "system",
                action,
                "NotificationAttempt",
                $"{request.EventId}:{channel}",
                new Dictionary<string, object?> { ["triggeringEventType"] = request.TriggeringEventType, ["userId"] = request.UserId }),
            cancellationToken);
}
