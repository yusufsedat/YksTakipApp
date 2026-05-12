namespace YksTakipApp.Core.Interfaces;

public sealed record AdaptationEvaluationJob(int UserId, int TopicId, int RecentExamScorePercent);

public interface IBackgroundTaskQueue
{
    int PendingCount { get; }
    ValueTask EnqueueAdaptationEvaluationAsync(AdaptationEvaluationJob job, CancellationToken ct = default);

    ValueTask<AdaptationEvaluationJob> DequeueAdaptationEvaluationAsync(CancellationToken ct);
}

public interface IBackgroundWorkerTelemetry
{
    bool IsRunning { get; }
    DateTimeOffset? LastHeartbeatUtc { get; }
    DateTimeOffset? LastFailureUtc { get; }
    string? LastError { get; }
    void MarkStarted();
    void MarkHeartbeat();
    void MarkFailure(Exception ex);
    void MarkStopped();
}
