using FluentValidation;

namespace Kart.Notification.Application.Features.UpsertNotificationPreference;

public sealed class UpsertNotificationPreferenceCommandValidator : AbstractValidator<UpsertNotificationPreferenceCommand>
{
    public UpsertNotificationPreferenceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OptOutMatrixJson).NotEmpty();
    }
}
