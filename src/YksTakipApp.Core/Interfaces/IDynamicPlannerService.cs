using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

public interface IDynamicPlannerService
{
    Task<PlanGenerationResult> GenerateWeeklyPlanAsync(int userId, DateOnly startDate, PlannerCallMetadata? metadata = null, CancellationToken ct = default);

    Task<IReadOnlyList<ScheduleTask>> GetWeeklyTasksAsync(int userId, DateOnly start, DateOnly end, CancellationToken ct = default);

    Task<ScheduleTask?> UpdateStatusAsync(int userId, int taskId, ScheduleTaskStatus status, CancellationToken ct = default);

    Task CheckAndTriggerChurnAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct = default);

    Task InvalidatePlannedWeekAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct = default);
}
