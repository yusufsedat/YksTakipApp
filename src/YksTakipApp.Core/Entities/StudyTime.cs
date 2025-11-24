namespace YksTakipApp.Core.Entities
{
    public class StudyTime
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public int DurationMinutes { get; set; } // örn. 90 dk

        public User User { get; set; } = null!;
    }
}
