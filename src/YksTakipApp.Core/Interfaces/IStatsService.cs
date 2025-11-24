namespace YksTakipApp.Core.Interfaces
{
    public interface IStatsService
    {
        Task<object> GetSummaryAsync(int userId);
        Task<object> GetWeeklyAsync(int userId);
        Task<object> GetProgressAsync(int userId);
    }
}
