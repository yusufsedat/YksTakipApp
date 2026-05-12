using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators;

public sealed class PlannerWeeklyQueryValidator : AbstractValidator<PlannerWeeklyQuery>
{
    public PlannerWeeklyQueryValidator()
    {
        RuleFor(x => x.End)
            .GreaterThanOrEqualTo(x => x.Start)
            .WithMessage("Bitiş tarihi başlangıçtan önce olamaz.");

        RuleFor(x => x)
            .Must(x => x.End.DayNumber - x.Start.DayNumber <= 13)
            .WithMessage("Tarih aralığı en fazla 14 gün olabilir.");
    }
}
