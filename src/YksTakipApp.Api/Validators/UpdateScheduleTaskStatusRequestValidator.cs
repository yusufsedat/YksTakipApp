using FluentValidation;
using YksTakipApp.Api.DTOs;
namespace YksTakipApp.Api.Validators;

public sealed class UpdateScheduleTaskStatusRequestValidator : AbstractValidator<UpdateScheduleTaskStatusRequest>
{
    public UpdateScheduleTaskStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("Geçersiz görev durumu.");
    }
}
