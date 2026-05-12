namespace YksTakipApp.Core.Models
{
    public sealed class UserGoalSnapshot
    {
        public Guid Id { get; init; }
        public string TargetUniversity { get; init; } = null!;
        public string TargetDepartment { get; init; } = null!;
        public decimal? TargetTytNet { get; init; }
        public decimal? TargetAytNet { get; init; }
        public int DailyAvailableMinutes { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public sealed class GoalStatusResult
    {
        public bool HasActiveGoal { get; init; }
        public bool CanSkip { get; init; }
        public UserGoalSnapshot? CurrentGoal { get; init; }
    }
}
