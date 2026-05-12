namespace YksTakipApp.Core.Entities
{
    /// <summary>Immutable hedef geçmişi; aktif sürüm <see cref="User.ActiveGoalVersionId"/> ile işaretlenir.</summary>
    public class UserGoal
    {
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public string TargetUniversity { get; set; } = null!;
        public string TargetDepartment { get; set; } = null!;
        public decimal? TargetTytNet { get; set; }
        public decimal? TargetAytNet { get; set; }
        /// <summary>Günlük çalışmaya ayırılabilir toplam dakika (buffer öncesi); planner %80'ini kullanır.</summary>
        public int DailyAvailableMinutes { get; set; } = 120;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
