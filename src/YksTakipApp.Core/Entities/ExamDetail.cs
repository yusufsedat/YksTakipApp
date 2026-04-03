namespace YksTakipApp.Core.Entities
{
    public class ExamDetail
    {
        public int Id { get; set; }
        public int ExamResultId { get; set; }
        public string Subject { get; set; } = null!;
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Blank { get; set; }
        public double Net { get; set; }

        public ExamResult ExamResult { get; set; } = null!;
    }
}
