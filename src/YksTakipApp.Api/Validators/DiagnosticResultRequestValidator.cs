using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators;

public sealed class DiagnosticResultRequestValidator : AbstractValidator<DiagnosticResultRequest>
{
    public DiagnosticResultRequestValidator()
    {
        RuleFor(x => x.Result)
            .NotEmpty()
            .Must(r =>
            {
                var k = r.ToLowerInvariant();
                return k is "passed" or "failed" or "skipped";
            })
            .WithMessage("Result: passed, failed veya skipped olmalıdır.");
    }
}
