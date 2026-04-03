namespace YksTakipApp.Api.DTOs
{
    public class ExamDetailInput
    {
        public string Subject { get; set; } = null!;
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Blank { get; set; }
    }

    public class ExamResultRequest
    {
        public string ExamName { get; set; } = null!;
        public string ExamType { get; set; } = "TYT"; // TYT, AYT, BRANS
        public string? Subject { get; set; } // BRANS icin
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public double NetTyt { get; set; }
        public double NetAyt { get; set; }
        public int? DurationMinutes { get; set; }
        public int? Difficulty { get; set; }
        public string? ErrorReasons { get; set; }
        public List<ExamDetailInput> Details { get; set; } = new();
    }
}
