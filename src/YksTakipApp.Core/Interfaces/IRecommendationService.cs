using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

public interface IRecommendationService
{
    Task<List<TopicPriorityDto>> GetDailyRecommendationsAsync(int userId, CancellationToken ct);
}
