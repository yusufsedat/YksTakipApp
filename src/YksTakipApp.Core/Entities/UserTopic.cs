using YksTakipApp.Core.Enums;

namespace YksTakipApp.Core.Entities
{
    public class UserTopic
    {
        public int UserId { get; set; }
        public int TopicId { get; set; }
        public TopicStatus Status { get; set; } = TopicStatus.NotStarted;

        public MasteryStatus MasteryStatus { get; set; } = MasteryStatus.NotStarted;
        public double MasteryConfidence { get; set; }
        public bool IsLocked { get; set; }
        public bool IsPriorityRequested { get; set; }
        public DateTime? PriorityRequestedAt { get; set; }
        public DateTime? PriorityExpiresAt { get; set; }
        public DateTime? PriorityResolvedAt { get; set; }
        public DateTime? LastEvaluatedAt { get; set; }

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
