namespace YksTakipApp.Core.Entities
{
    public class StudyTime
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public int DurationMinutes { get; set; } // örn. 90 dk

        /// <summary>İsteğe bağlı: çalışmanın bağlandığı konu (kullanıcı listesinde olmalı).</summary>
        public int? TopicId { get; set; }
        public Topic? Topic { get; set; }

        public User User { get; set; } = null!;
    }
}
