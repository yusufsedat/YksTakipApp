using YksTakipApp.Core.Models;

namespace YksTakipApp.Api.DTOs;

public sealed class PlanGenerationResponse
{
    public PlanGenerationStatus Status { get; init; } = PlanGenerationStatus.Success;
    public PlanGenerationReasonCode ReasonCode { get; init; } = PlanGenerationReasonCode.None;
    public string? Message { get; init; }
    public int? CurrentMinutes { get; init; }
    public int? MinimumRequiredMinutes { get; init; }
    public IReadOnlyList<ScheduleTaskDto> Tasks { get; init; } = Array.Empty<ScheduleTaskDto>();
}
