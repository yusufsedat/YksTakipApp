using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class ExamDetailInputValidator : AbstractValidator<ExamDetailInput>
    {
        public ExamDetailInputValidator()
        {
            RuleFor(x => x.Subject).NotEmpty().MaximumLength(60);
            RuleFor(x => x.Correct).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Wrong).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Blank).GreaterThanOrEqualTo(0);
        }
    }

    public class ExamResultRequestValidator : AbstractValidator<ExamResultRequest>
    {
        private static readonly string[] ValidTypes = { "TYT", "AYT", "YDT", "BRANS" };

        public ExamResultRequestValidator()
        {
            RuleFor(x => x.ExamName)
                .NotEmpty()
                .Length(2, 120);

            RuleFor(x => x.ExamType)
                .NotEmpty()
                .Must(t => ValidTypes.Contains(t))
                .WithMessage("ExamType TYT, AYT, YDT veya BRANS olmalıdır.");

            RuleFor(x => x.Subject)
                .NotEmpty()
                .When(x => x.ExamType == "BRANS")
                .WithMessage("Branş denemesi için Subject alanı zorunludur.");

            RuleFor(x => x.Date)
                .NotEmpty()
                .Must(date => date != default)
                .WithMessage("Date alanı geçerli bir tarih olmalıdır.");

            RuleFor(x => x.NetTyt).InclusiveBetween(0, 200);
            RuleFor(x => x.NetAyt).InclusiveBetween(0, 200);

            RuleFor(x => x.DurationMinutes)
                .InclusiveBetween(1, 600)
                .When(x => x.DurationMinutes.HasValue);

            RuleFor(x => x.Difficulty)
                .InclusiveBetween(1, 5)
                .When(x => x.Difficulty.HasValue);

            RuleForEach(x => x.Details).SetValidator(new ExamDetailInputValidator());
        }
    }
}
