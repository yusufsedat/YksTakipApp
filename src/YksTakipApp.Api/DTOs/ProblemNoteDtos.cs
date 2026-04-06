namespace YksTakipApp.Api.DTOs
{
    public class ProblemNoteDto
    {
        public int Id { get; set; }
        /// <summary>Cloudinary HTTPS URL veya eski kayıt (base64).</summary>
        public string ImageUrl { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public bool SolutionLearned { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProblemNoteCreateRequest
    {
        public string ImageBase64 { get; set; } = "";
        public List<string>? Tags { get; set; }
        public bool SolutionLearned { get; set; }
    }

    public class ProblemNoteUpdateRequest
    {
        public List<string>? Tags { get; set; }
        public bool SolutionLearned { get; set; }
        public string? ImageBase64 { get; set; }
    }
}
