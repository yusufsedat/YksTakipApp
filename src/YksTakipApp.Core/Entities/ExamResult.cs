namespace YksTakipApp.Core.Entities
{
    public class ExamResult
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string ExamName { get; set; } = null!;
        public DateTime Date { get; set; }
        public double NetTyt { get; set; }
        public double NetAyt { get; set; }

        public User User { get; set; } = null!;
    }
}
