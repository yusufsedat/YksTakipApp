namespace YksTakipApp.Core.Entities
{
    public class UserTopic
    {
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public TopicStatus Status { get; set; } = TopicStatus.NotStarted;

        // Navigation
        public User User { get; set; } = null!;
        public Topic Topic { get; set; } = null!;
    }

    public enum TopicStatus
    {
        NotStarted,
        InProgress,
        Completed,
        NeedsReview
    }
}
