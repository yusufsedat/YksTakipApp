using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class ScheduleCreateRequestValidator : AbstractValidator<ScheduleCreateRequest>
    {
        public ScheduleCreateRequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Recurrence)
                .Must(r => r.Equals("Weekly", StringComparison.OrdinalIgnoreCase) ||
                           r.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Recurrence Weekly veya Monthly olmalı.");

            RuleFor(x => x.StartMinute).InclusiveBetween(0, 1439);
            RuleFor(x => x.EndMinute).InclusiveBetween(0, 1439);
            RuleFor(x => x)
                .Must(x => x.EndMinute > x.StartMinute)
                .WithMessage("Bitiş saati başlangıçtan sonra olmalı.");

            When(x => string.Equals(x.Recurrence, "Weekly", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.DayOfWeek).NotNull().InclusiveBetween(0, 6);
                RuleFor(x => x.DayOfMonth).Null();
            });

            When(x => string.Equals(x.Recurrence, "Monthly", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.DayOfMonth).NotNull().InclusiveBetween(1, 31);
                RuleFor(x => x.DayOfWeek).Null();
            });

            When(x => x.TopicId.HasValue, () => RuleFor(x => x.TopicId!.Value).GreaterThan(0));
        }
    }

    public class ScheduleUpdateRequestValidator : AbstractValidator<ScheduleUpdateRequest>
    {
        public ScheduleUpdateRequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Recurrence)
                .Must(r => r.Equals("Weekly", StringComparison.OrdinalIgnoreCase) ||
                           r.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Recurrence Weekly veya Monthly olmalı.");

            RuleFor(x => x.StartMinute).InclusiveBetween(0, 1439);
            RuleFor(x => x.EndMinute).InclusiveBetween(0, 1439);
            RuleFor(x => x)
                .Must(x => x.EndMinute > x.StartMinute)
                .WithMessage("Bitiş saati başlangıçtan sonra olmalı.");

            When(x => string.Equals(x.Recurrence, "Weekly", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.DayOfWeek).NotNull().InclusiveBetween(0, 6);
                RuleFor(x => x.DayOfMonth).Null();
            });

            When(x => string.Equals(x.Recurrence, "Monthly", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.DayOfMonth).NotNull().InclusiveBetween(1, 31);
                RuleFor(x => x.DayOfWeek).Null();
            });

            When(x => x.TopicId.HasValue, () => RuleFor(x => x.TopicId!.Value).GreaterThan(0));
        }
    }
}
