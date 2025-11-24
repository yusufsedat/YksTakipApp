namespace YksTakipApp.Core.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<UserTopic> UserTopics { get; set; } = new List<UserTopic>();
        public ICollection<StudyTime> StudyTimes { get; set; } = new List<StudyTime>();
        public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
    }
}
