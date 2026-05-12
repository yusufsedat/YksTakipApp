namespace YksTakipApp.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        /// <summary>JWT ve yetkilendirme: "User" veya "Admin".</summary>
        public string Role { get; set; } = "User";
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int SmartOnboardingSkipCount { get; set; }
        public Guid? ActiveGoalVersionId { get; set; }

        // Navigation
        public ICollection<UserTopic> UserTopics { get; set; } = new List<UserTopic>();
        public ICollection<StudyTime> StudyTimes { get; set; } = new List<StudyTime>();
        public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
        public ICollection<ProblemNote> ProblemNotes { get; set; } = new List<ProblemNote>();
        public ICollection<UserGoal> UserGoals { get; set; } = new List<UserGoal>();
        public ICollection<ScheduleTask> ScheduleTasks { get; set; } = new List<ScheduleTask>();
    }
}
