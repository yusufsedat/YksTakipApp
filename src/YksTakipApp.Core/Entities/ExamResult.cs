namespace YksTakipApp.Core.Entities
{
    public class ExamResult
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ExamName { get; set; } = null!;
        public string ExamType { get; set; } = "TYT"; // TYT, AYT, BRANS
        public string? Subject { get; set; } // BRANS icin ders adi
        public DateTime Date { get; set; }
        public double NetTyt { get; set; }
        public double NetAyt { get; set; }
        public int? DurationMinutes { get; set; }
        public int? Difficulty { get; set; } // 1-5
        public string? ErrorReasons { get; set; } // virgul-ayrimli
        public int? TopicId { get; set; }

        public User User { get; set; } = null!;
        public Topic? Topic { get; set; }
        public ICollection<ExamDetail> ExamDetails { get; set; } = new List<ExamDetail>();
    }
}
