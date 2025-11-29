namespace YksTakipApp.Api.DTOs
{
    public class LoginRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; } = null!;

        [System.ComponentModel.DataAnnotations.Required]
        public string Password { get; set; } = null!;
    }
}
