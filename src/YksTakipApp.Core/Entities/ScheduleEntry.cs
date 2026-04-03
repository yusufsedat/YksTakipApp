namespace YksTakipApp.Core.Entities
{
    /// <summary>
    /// Kullanıcı ders programı: haftalık (haftanın günü) veya aylık (ayın günü) tekrar.
    /// </summary>
    public class ScheduleEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        /// <summary>Weekly veya Monthly</summary>
        public string Recurrence { get; set; } = "Weekly";

        /// <summary>Haftalık: 0=Pazar … 6=Cumartesi (DateTime.DayOfWeek ile uyumlu).</summary>
        public int? DayOfWeek { get; set; }

        /// <summary>Aylık: 1–31</summary>
        public int? DayOfMonth { get; set; }

        /// <summary>Gün içi başlangıç dakikası (0–1439).</summary>
        public int StartMinute { get; set; }

        /// <summary>Gün içi bitiş dakikası (0–1439), başlangıçtan büyük olmalı.</summary>
        public int EndMinute { get; set; }

        public string Title { get; set; } = "";

        /// <summary>İsteğe bağlı: Konular tablosundaki konu (kullanıcı listesinde olmalı).</summary>
        public int? TopicId { get; set; }

        public User User { get; set; } = null!;
        public Topic? Topic { get; set; }
    }
}
