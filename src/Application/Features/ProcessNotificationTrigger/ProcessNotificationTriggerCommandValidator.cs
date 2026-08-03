using FluentValidation;
using Kart.Notification.Domain.Catalog;

namespace Kart.Notification.Application.Features.ProcessNotificationTrigger;

public sealed class ProcessNotificationTriggerCommandValidator : AbstractValidator<ProcessNotificationTriggerCommand>
{
    public ProcessNotificationTriggerCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.RetryCount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TriggeringEventType)
            .NotEmpty()
            .Must(eventType => TriggeringEventCatalog.TryGet(eventType, out _))
            .WithMessage("'{PropertyValue}' is not a recognized triggering event type (ADR-0003's approved scope).");
    }
}
