namespace YksTakipApp.Core.Entities;

/// <summary>DAG edge: <see cref="TopicId"/> depends on <see cref="PrerequisiteTopicId"/>.</summary>
public class TopicPrerequisite
{
    public int Id { get; set; }
    public int TopicId { get; set; }
    public int PrerequisiteTopicId { get; set; }

    public Topic Topic { get; set; } = null!;
    public Topic PrerequisiteTopic { get; set; } = null!;
}
