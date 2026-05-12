using YksTakipApp.Core.Enums;

namespace YksTakipApp.Core.Entities;

public class ScheduleTask
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TopicId { get; set; }
    public DateOnly TaskDate { get; set; }
    public int DurationMinutes { get; set; }
    public ScheduleTaskStatus Status { get; set; } = ScheduleTaskStatus.Planned;
    public bool IsRecoveryTask { get; set; }
    public TaskType TaskType { get; set; } = TaskType.Study;
    public string? Reason { get; set; }
    /// <summary>Ön koşul konusu (teşhis görevinin hedefi).</summary>
    public int? PrerequisiteTopicId { get; set; }
    /// <summary>Düşük performans sinyalinin geldiği ana konu.</summary>
    public int? MainTopicId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Topic Topic { get; set; } = null!;
}
