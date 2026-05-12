namespace YksTakipApp.Core.Entities;

public enum PlannerChurnReasonCode
{
    NoPlannedToday = 0,
    NoStudyTaskInWeek = 1
}

public sealed class UserPlannerChurnEvent
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }
    public DateOnly TriggerDate { get; set; }
    public PlannerChurnReasonCode ReasonCode { get; set; }
    public int? DaysSinceLastCompletedTask { get; set; }
    public int? DaysSincePlanGenerated { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
