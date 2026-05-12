using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators;

public sealed class GenerateWeeklyPlanRequestValidator : AbstractValidator<GenerateWeeklyPlanRequest>
{
    public GenerateWeeklyPlanRequestValidator()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        RuleFor(x => x.StartDate)
            .Must(d => d >= today.AddDays(-7))
            .WithMessage("Başlangıç tarihi son 7 günden eski olamaz.")
            .Must(d => d <= today.AddDays(90))
            .WithMessage("Başlangıç tarihi en fazla 90 gün ileride olabilir.");

        RuleFor(x => x.ClientRequestId)
            .MaximumLength(120)
            .When(x => !string.IsNullOrWhiteSpace(x.ClientRequestId));
    }
}
