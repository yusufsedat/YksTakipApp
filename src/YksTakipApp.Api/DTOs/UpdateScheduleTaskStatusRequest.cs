using YksTakipApp.Core.Enums;

namespace YksTakipApp.Api.DTOs;

public sealed class UpdateScheduleTaskStatusRequest
{
    public ScheduleTaskStatus Status { get; set; }
}
