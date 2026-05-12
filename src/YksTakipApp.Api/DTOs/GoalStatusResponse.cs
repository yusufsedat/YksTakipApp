namespace YksTakipApp.Api.DTOs
{
    public sealed class GoalStatusResponse
    {
        public bool HasActiveGoal { get; set; }
        public bool CanSkip { get; set; }
        public UserGoalDto? CurrentGoal { get; set; }
    }
}
