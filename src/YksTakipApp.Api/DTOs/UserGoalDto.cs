namespace YksTakipApp.Api.DTOs
{
    public sealed class UserGoalDto
    {
        public Guid Id { get; set; }
        public string TargetUniversity { get; set; } = null!;
        public string TargetDepartment { get; set; } = null!;
        public decimal? TargetTytNet { get; set; }
        public decimal? TargetAytNet { get; set; }
        public int DailyAvailableMinutes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
