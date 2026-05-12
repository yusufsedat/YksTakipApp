using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.BackgroundWorkers;

public sealed class AdaptationWorker : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IBackgroundWorkerTelemetry _telemetry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdaptationWorker> _log;

    public AdaptationWorker(
        IBackgroundTaskQueue queue,
        IBackgroundWorkerTelemetry telemetry,
        IServiceScopeFactory scopeFactory,
        ILogger<AdaptationWorker> log)
    {
        _queue = queue;
        _telemetry = telemetry;
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _telemetry.MarkStarted();
        while (!stoppingToken.IsCancellationRequested)
        {
            AdaptationEvaluationJob job;
            try
            {
                job = await _queue.DequeueAdaptationEvaluationAsync(stoppingToken);
                _telemetry.MarkHeartbeat();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var adaptation = scope.ServiceProvider.GetRequiredService<IAdaptationService>();
                await adaptation.EvaluateTopicPerformanceAsync(
                    job.UserId,
                    job.TopicId,
                    job.RecentExamScorePercent,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _telemetry.MarkFailure(ex);
                _log.LogError(
                    ex,
                    "Adaptation worker failed for user {UserId}, topic {TopicId}.",
                    job.UserId,
                    job.TopicId);
            }
        }
        _telemetry.MarkStopped();
    }
}
