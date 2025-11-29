using FluentValidation;
using YksTakipApp.Api.DTOs;

namespace YksTakipApp.Api.Validators
{
    public class UserTopicUpdateRequestValidator : AbstractValidator<UserTopicUpdateRequest>
    {
        public UserTopicUpdateRequestValidator()
        {
            RuleFor(x => x.TopicId)
                .GreaterThan(0);

            RuleFor(x => x.Status)
                .IsInEnum();
        }
    }
}

