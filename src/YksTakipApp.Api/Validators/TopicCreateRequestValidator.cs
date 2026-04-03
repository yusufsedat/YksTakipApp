using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class TopicCreateRequestValidator : AbstractValidator<TopicCreateRequest>
    {
        private static readonly string[] AllowedCategories = ["TYT", "AYT"];

        public TopicCreateRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .Length(2, 120);

            RuleFor(x => x.Category)
                .NotEmpty()
                .Must(category => AllowedCategories.Contains(category))
                .WithMessage("Category TYT veya AYT olmalıdır.");

            RuleFor(x => x.Subject)
                .MaximumLength(60);
        }
    }
}

