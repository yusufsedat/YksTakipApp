using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class ExamResultRequestValidator : AbstractValidator<ExamResultRequest>
    {
        public ExamResultRequestValidator()
        {
            RuleFor(x => x.ExamName)
                .NotEmpty()
                .Length(2, 120);

            RuleFor(x => x.Date)
                .NotEmpty()
                .Must(date => date != default)
                .WithMessage("Date alanı geçerli bir tarih olmalıdır.");

            RuleFor(x => x.NetTyt)
                .InclusiveBetween(0, 120);

            RuleFor(x => x.NetAyt)
                .InclusiveBetween(0, 120);
        }
    }
}

