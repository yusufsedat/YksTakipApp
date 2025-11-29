namespace YksTakipApp.Api.DTOs
{
    public class ExamResultRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 2)]
        public string ExamName { get; set; } = null!;
        public DateTime Date { get; set; } = DateTime.UtcNow;
        [System.ComponentModel.DataAnnotations.Range(0, 120)]
        public double NetTyt { get; set; }
        [System.ComponentModel.DataAnnotations.Range(0, 120)]
        public double NetAyt { get; set; }
    }
}
