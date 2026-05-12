using Microsoft.Extensions.Logging;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class PlannerDecisionLogger : IPlannerDecisionLogger
{
    private readonly AppDbContext _db;
    private readonly ILogger<PlannerDecisionLogger> _log;

    public PlannerDecisionLogger(AppDbContext db, ILogger<PlannerDecisionLogger> log)
    {
        _db = db;
        _log = log;
    }

    public async Task LogAsync(PlannerDecisionContext context, CancellationToken ct)
    {
        var entity = new PlannerDecisionLog
        {
            UserId = context.UserId,
            WeekStart = context.WeekStart,
            WeekEnd = context.WeekEnd,
            Status = context.Status,
            ReasonCode = context.ReasonCode,
            TaskCountTotal = context.TaskCountTotal,
            TaskCountStudy = context.TaskCountStudy,
            TaskCountReview = context.TaskCountReview,
            TaskCountDiagnostic = context.TaskCountDiagnostic,
            PreservedTaskCount = context.PreservedTaskCount,
            RecommendationCandidateCount = context.RecommendationCandidateCount,
            RecommendationScheduledCount = context.RecommendationScheduledCount,
            RecommendationSkippedByCapacityCount = context.RecommendationSkippedByCapacityCount,
            RecommendationSkippedByDuplicateCount = context.RecommendationSkippedByDuplicateCount,
            DailyCapacity = context.DailyCapacity,
            WorkingDaily = context.WorkingDaily,
            BufferDaily = context.BufferDaily,
            EffectiveCapacityMultiplier = context.EffectiveCapacityMultiplier,
            DynamicBufferRate = context.DynamicBufferRate,
            PriorityActiveCount = context.PriorityActiveCount,
            PriorityPlacedCount = context.PriorityPlacedCount,
            InjectedReviewTaskCount = context.InjectedReviewTaskCount,
            QualityScore = context.QualityScore?.Total,
            QualityBand = context.QualityScore?.Band,
            BreakdownJson = context.BreakdownJson,
            CorrelationId = context.CorrelationId,
            IdempotencyKey = context.IdempotencyKey,
            DurationMs = context.DurationMs,
            CreatedAt = context.CreatedAt
        };

        try
        {
            _db.PlannerDecisionLogs.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "PlannerDecisionLogger persist failed (swallowed). UserId={UserId} WeekStart={WeekStart} Status={Status} ReasonCode={ReasonCode} CorrelationId={CorrelationId} IdempotencyKey={IdempotencyKey} DurationMs={DurationMs}",
                context.UserId, context.WeekStart, context.Status, context.ReasonCode,
                context.CorrelationId, context.IdempotencyKey, context.DurationMs);

            _db.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
    }
}
