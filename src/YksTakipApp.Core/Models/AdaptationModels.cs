using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;

namespace YksTakipApp.Core.Models;

public enum DiagnosticResult
{
    Passed,
    Failed,
    SkippedOrDeleted
}

public enum DiagnosticOutcome
{
    Cleared,
    TopicLocked,
    PrerequisiteDowngraded,
    /// <summary>Görev zaten tamamlanmış veya atlanmış; iş kuralı tekrar uygulanmadı.</summary>
    NoChange
}

public sealed class TopicProgressDto
{
    public int TopicId { get; set; }
    public MasteryStatus MasteryStatus { get; set; }
    public double MasteryConfidence { get; set; }
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }
}

public sealed class AdaptationRecordResult
{
    public DiagnosticOutcome Outcome { get; init; }
    public required ScheduleTask ScheduleTask { get; init; }
}
