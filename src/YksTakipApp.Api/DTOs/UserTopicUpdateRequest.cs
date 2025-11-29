using YksTakipApp.Core.Entities;

namespace YksTakipApp.Api.DTOs
{
    public class UserTopicUpdateRequest
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int TopicId { get; set; }
        public TopicStatus Status { get; set; }
    }
}
