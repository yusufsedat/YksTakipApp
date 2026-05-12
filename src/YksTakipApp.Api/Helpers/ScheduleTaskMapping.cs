using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Api.Helpers;

public static class ScheduleTaskMapping
{
    public static ScheduleTaskDto ToDto(ScheduleTask t) =>
        new()
        {
            Id = t.Id,
            TopicId = t.TopicId,
            TopicName = t.Topic?.Name ?? "",
            SubjectName = t.Topic?.Subject ?? "",
            TaskDate = t.TaskDate,
            DurationMinutes = t.DurationMinutes,
            Status = t.Status,
            IsRecoveryTask = t.IsRecoveryTask,
            TaskType = t.TaskType,
            Reason = t.Reason,
            PrerequisiteTopicId = t.PrerequisiteTopicId,
            MainTopicId = t.MainTopicId
        };

    public static PlanGenerationResponse ToResponse(PlanGenerationResult result) =>
        new()
        {
            Status = result.Status,
            ReasonCode = result.ReasonCode,
            Message = result.Message,
            CurrentMinutes = result.CurrentMinutes,
            MinimumRequiredMinutes = result.MinimumRequiredMinutes,
            Tasks = result.Tasks.Select(ToDto).ToList()
        };
}
