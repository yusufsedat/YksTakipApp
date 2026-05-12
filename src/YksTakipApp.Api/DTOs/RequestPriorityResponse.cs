namespace YksTakipApp.Api.DTOs;

public sealed class RequestPriorityResponse
{
    public string Message { get; init; } = "";
    public PlanGenerationResponse Plan { get; init; } = new();
    public IReadOnlyList<ScheduleTaskDto> Tasks { get; init; } = Array.Empty<ScheduleTaskDto>();
}
