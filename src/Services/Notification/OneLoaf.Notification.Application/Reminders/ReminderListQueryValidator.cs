using FluentValidation;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Text;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>Validates <see cref="ReminderListQuery"/>.</summary>
public sealed class ReminderListQueryValidator : AbstractValidator<ReminderListQuery>
{
    /// <summary>Creates the rules.</summary>
    public ReminderListQueryValidator()
    {
        RuleFor(x => x.Status).Must(WireEnumNames.IsNullOrDefined<ReminderStatus>)
            .WithMessage($"status must be one of: {WireEnumNames.Values<ReminderStatus>()}.");
        RuleFor(x => x.Limit).InclusiveBetween(1, ReminderListQuery.MaxLimit);
    }
}
