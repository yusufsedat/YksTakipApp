using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class StudyTimeRequestValidator : AbstractValidator<StudyTimeRequest>
    {
        public StudyTimeRequestValidator()
        {
            RuleFor(x => x.DurationMinutes)
                .InclusiveBetween(1, 1440);

            RuleFor(x => x.Date)
                .NotEmpty()
                .Must(date => date != default)
                .WithMessage("Date alanı geçerli bir tarih olmalıdır.");
        }
    }
}

