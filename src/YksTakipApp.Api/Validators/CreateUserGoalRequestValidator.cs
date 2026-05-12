using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public sealed class CreateUserGoalRequestValidator : AbstractValidator<CreateUserGoalRequest>
    {
        public CreateUserGoalRequestValidator()
        {
            RuleFor(x => x.TargetUniversity)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.TargetDepartment)
                .NotEmpty()
                .MaximumLength(200);

            When(x => x.TargetTytNet.HasValue, () =>
            {
                RuleFor(x => x.TargetTytNet!.Value)
                    .InclusiveBetween(0m, 120m)
                    .PrecisionScale(5, 2, false);
            });

            When(x => x.TargetAytNet.HasValue, () =>
            {
                RuleFor(x => x.TargetAytNet!.Value)
                    .InclusiveBetween(0m, 80m)
                    .PrecisionScale(5, 2, false);
            });

            RuleFor(x => x.DailyAvailableMinutes)
                .InclusiveBetween(30, 1440);
        }
    }
}
