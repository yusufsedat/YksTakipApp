using YksTakipApp.Core.Enums;

namespace YksTakipApp.Api.DTOs;

public sealed class ScheduleTaskDto
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public string TopicName { get; set; } = "";
    public string SubjectName { get; set; } = "";
    public DateOnly TaskDate { get; set; }
    public int DurationMinutes { get; set; }
    public ScheduleTaskStatus Status { get; set; }
    public bool IsRecoveryTask { get; set; }
    public TaskType TaskType { get; set; }
    public string? Reason { get; set; }
    public int? PrerequisiteTopicId { get; set; }
    public int? MainTopicId { get; set; }
}
