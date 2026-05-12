using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

public interface IAnalyticsService
{
    Task<ChurnSummaryDto> GetChurnSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<FeedbackLoopSummaryDto> GetFeedbackSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<FeedbackLoopUserDto> GetFeedbackForUserAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default);
}
