namespace YksTakipApp.Api.DTOs
{
    public class TopicCreateRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.RegularExpression("^(TYT|AYT)$", ErrorMessage = "Category TYT veya AYT olmalıdır.")]
        public string Category { get; set; } = null!; // TYT veya AYT
    }
}
