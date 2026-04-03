namespace YksTakipApp.Core.Interfaces
{
    public interface IStatsService
    {
        Task<object> GetSummaryAsync(int userId);
        Task<object> GetWeeklyAsync(int userId);
        Task<object> GetProgressAsync(int userId);
        Task<object> GetTytStatsAsync(int userId);
        Task<object> GetAytStatsAsync(int userId);
        Task<object> GetBransStatsAsync(int userId);

        /// <summary>Üst üste deneme çözülen gün sayısı (UTC takvim günü).</summary>
        Task<int> GetExamStreakDaysAsync(int userId);

        /// <summary>Seri + derse göre tamamlanan konu sayıları (gamification).</summary>
        Task<object> GetWinsAsync(int userId);
    }
}
