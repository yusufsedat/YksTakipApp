namespace YksTakipApp.Core.Models;

/// <summary>
/// Admin debug aggregate: planner cinsinden user'ın anlık durumu. UI yok; JSON döner.
/// </summary>
public sealed class PlannerDebugSnapshot
{
    public PlannerDebugUserDto? User { get; init; }
    public PlannerDebugGoalDto? ActiveGoal { get; init; }
    public PlannerDebugCapacityDto Capacity { get; init; } = new();
    public string? Segment { get; init; }
    public IReadOnlyList<PlannerDebugTaskDto> LatestPlan { get; init; } = Array.Empty<PlannerDebugTaskDto>();
    public PlannerDebugDecisionLogDto? LatestDecisionLog { get; init; }
    public IReadOnlyList<PlannerDebugPriorityRequestDto> PriorityRequests { get; init; } = Array.Empty<PlannerDebugPriorityRequestDto>();
    public IReadOnlyList<PlannerDebugChurnEventDto> RecentChurnEvents { get; init; } = Array.Empty<PlannerDebugChurnEventDto>();
    public IReadOnlyDictionary<string, bool> FeatureFlags { get; init; } = new Dictionary<string, bool>();
}

public sealed record PlannerDebugUserDto(int Id, string Name, string Role, Guid? ActiveGoalVersionId, DateTime CreatedAt);
public sealed record PlannerDebugGoalDto(Guid Id, int DailyAvailableMinutes, decimal? TargetTytNet, decimal? TargetAytNet, DateTime CreatedAt);
public sealed class PlannerDebugCapacityDto
{
    public int DailyAvailableMinutes { get; init; }
    public double EffectiveCapacityMultiplier { get; init; }
    public double DynamicBufferRate { get; init; }
    public int WorkingDaily { get; init; }
}
public sealed record PlannerDebugTaskDto(long Id, int TopicId, string? TopicName, DateOnly TaskDate, int DurationMinutes, string Status, string TaskType);
public sealed record PlannerDebugDecisionLogDto(
    long Id,
    DateOnly WeekStart,
    PlanGenerationStatus Status,
    PlanGenerationReasonCode ReasonCode,
    int TaskCountTotal,
    int? QualityScore,
    int DurationMs,
    DateTime CreatedAt,
    string BreakdownJson);
public sealed record PlannerDebugPriorityRequestDto(int TopicId, string? TopicName, DateTime? RequestedAt, DateTime? ExpiresAt, DateTime? ResolvedAt);
public sealed record PlannerDebugChurnEventDto(long Id, DateOnly TriggerDate, int ReasonCode, DateTime CreatedAt);
