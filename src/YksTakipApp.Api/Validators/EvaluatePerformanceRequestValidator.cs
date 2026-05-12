using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators;

public sealed class EvaluatePerformanceRequestValidator : AbstractValidator<EvaluatePerformanceRequest>
{
    public EvaluatePerformanceRequestValidator()
    {
        RuleFor(x => x.TopicId).GreaterThan(0);
        RuleFor(x => x.RecentExamScorePercent).InclusiveBetween(0, 100);
    }
}
