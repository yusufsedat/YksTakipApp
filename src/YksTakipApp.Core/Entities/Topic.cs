namespace YksTakipApp.Core.Entities
{
    public class Topic
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!; // TYT / AYT

        public ICollection<UserTopic> UserTopics { get; set; } = new List<UserTopic>();
    }
}
