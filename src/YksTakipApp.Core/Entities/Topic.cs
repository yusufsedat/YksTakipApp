namespace YksTakipApp.Core.Entities
{
    public class Topic
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!; // TYT / AYT
        /// <summary>Branş: Türkçe, Matematik, Fen Bilimleri, vb. (liste ve filtreleme için).</summary>
        public string Subject { get; set; } = "";

        public ICollection<UserTopic> UserTopics { get; set; } = new List<UserTopic>();
    }
}
