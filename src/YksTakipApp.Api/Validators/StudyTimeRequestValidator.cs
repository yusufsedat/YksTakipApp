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
                .WithMessage("Date alanı geçerli bir tarih olmalıdır.")
                .Must(date =>
                {
                    var u = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
                    return u.Date <= DateTime.UtcNow.Date;
                })
                .WithMessage("Gelecek tarihli çalışma kaydı eklenemez.");

            RuleFor(x => x.TopicId)
                .Must(id => id is null or > 0)
                .WithMessage("Geçerli bir konu seçin.");
        }
    }
}

