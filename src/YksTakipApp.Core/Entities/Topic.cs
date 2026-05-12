namespace YksTakipApp.Core.Entities
{
    public class Topic
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!; // TYT / AYT
        /// <summary>Branş: Türkçe, Matematik, Fen Bilimleri, vb. (liste ve filtreleme için).</summary>
        public string Subject { get; set; } = "";

        /// <summary>ÖSYM ağırlığı; öneri skorunda 1.0–1.5 arası çarpan olarak kullanılır.</summary>
        public double OsymWeight { get; set; } = 1.0;

        public ICollection<UserTopic> UserTopics { get; set; } = new List<UserTopic>();

        /// <summary>Edges where this topic depends on prerequisites (<see cref="TopicPrerequisite.TopicId"/> == <see cref="Id"/>).</summary>
        public ICollection<TopicPrerequisite> Prerequisites { get; set; } = new List<TopicPrerequisite>();

        /// <summary>Edges where other topics list this topic as a prerequisite (<see cref="TopicPrerequisite.PrerequisiteTopicId"/> == <see cref="Id"/>).</summary>
        public ICollection<TopicPrerequisite> Dependents { get; set; } = new List<TopicPrerequisite>();
    }
}
