namespace YksTakipApp.Api.DTOs
{
    public class RegisterRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 2)]
        public string Name { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        [System.ComponentModel.DataAnnotations.StringLength(200)]
        public string Email { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = null!;
    }
}
