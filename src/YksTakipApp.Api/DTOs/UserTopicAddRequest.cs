namespace YksTakipApp.Api.DTOs
{
    public class UserTopicAddRequest
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue, ErrorMessage = "TopicId geçerli bir değer olmalıdır.")]
        public int TopicId { get; set; }
    }
}

