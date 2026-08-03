using MediatR;

namespace Kart.Notification.Application.Features.ProcessNotificationTrigger;

/// <summary>
/// NOTIF-3's uniform, producer-agnostic creation path (ddd-model.md Modeling Decision #10) — every
/// one of ADR-0003's 13 triggering events is reduced to this same shape by its own consumer
/// dispatcher (`Infrastructure.Messaging`) before this single pipeline runs. <paramref name="RetryCount"/>
/// is the 0-indexed attempt number for the *whole inbound message* (from its retry-count header,
/// 0 on first delivery) - used only to size the next retry-tier index if a retry is still needed;
/// the actual budget-exhaustion decision is gated per-channel against
/// <see cref="Domain.Catalog.TriggeringEventCatalog"/>'s own tier ceiling, not this count directly.
/// </summary>
public sealed record ProcessNotificationTriggerCommand(
    Guid EventId,
    Guid UserId,
    string TriggeringEventType,
    int RetryCount) : IRequest<ProcessNotificationTriggerResult>;
