namespace YksTakipApp.Api.DTOs
{
    public class StudyTimeBulkCreateRequest
    {
        public List<StudyTimeRequest> Items { get; set; } = new();
    }
}
