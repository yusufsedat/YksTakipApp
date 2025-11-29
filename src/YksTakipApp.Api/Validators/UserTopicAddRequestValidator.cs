using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class UserTopicAddRequestValidator : AbstractValidator<UserTopicAddRequest>
    {
        public UserTopicAddRequestValidator()
        {
            RuleFor(x => x.TopicId)
                .GreaterThan(0)
                .WithMessage("TopicId 0'dan büyük olmalıdır.");
        }
    }
}

