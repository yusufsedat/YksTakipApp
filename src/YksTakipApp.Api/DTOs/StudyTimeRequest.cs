namespace YksTakipApp.Api.DTOs
{
    public class StudyTimeRequest
    {
        public DateTime Date { get; set; } = DateTime.UtcNow;
        [System.ComponentModel.DataAnnotations.Range(1, 1440)]
        public int DurationMinutes { get; set; }
    }
}
