using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

public interface IAdaptationService
{
    Task EvaluateTopicPerformanceAsync(int userId, int topicId, int recentExamScorePercent, CancellationToken ct = default);

    Task<AdaptationRecordResult?> RecordDiagnosticTestResultAsync(int userId, int taskId, DiagnosticResult result, CancellationToken ct = default);

    Task<TopicProgressDto?> GetTopicProgressAsync(int userId, int topicId, CancellationToken ct = default);
}
