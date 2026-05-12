using System.Text.Json;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Application.Services;

public sealed class PlannerDecisionContextBuilder : IPlannerDecisionContextBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public PlannerDecisionContext Build(PlannerDecisionInput input)
    {
        var breakdown = new
        {
            capacity = new
            {
                dailyCap = input.DailyCapacity,
                workingDaily = input.WorkingDaily,
                bufferDaily = input.BufferDaily,
                multiplier = input.EffectiveCapacityMultiplier,
                bufferRate = input.DynamicBufferRate
            },
            priority = new
            {
                activeCount = input.PriorityActiveCount,
                placedCount = input.PriorityPlacedCount,
                skippedTopicIds = input.PrioritySkippedTopicIds
            },
            recommendationSummary = new
            {
                candidateCount = input.RecommendationCandidateCount,
                scheduledCount = input.RecommendationScheduledCount,
                skippedByCapacity = input.RecommendationSkippedByCapacityCount,
                skippedByDuplicate = input.RecommendationSkippedByDuplicateCount,
                topPriorityScore = input.TopRecommendationPriorityScore
            },
            perDayRemaining = input.PerDayRemaining,
            qualityComponents = input.QualityScore is null ? null : new
            {
                total = input.QualityScore.Total,
                band = input.QualityScore.Band.ToString(),
                capacityFit = input.QualityScore.CapacityFit,
                priorityCoverage = input.QualityScore.PriorityCoverage,
                weaknessCoverage = input.QualityScore.WeaknessCoverage,
                subjectBalance = input.QualityScore.SubjectBalance,
                repetitionSafety = input.QualityScore.RepetitionSafety,
                overloadSafety = input.QualityScore.OverloadSafety
            }
        };

        var json = JsonSerializer.Serialize(breakdown, JsonOpts);

        return new PlannerDecisionContext
        {
            UserId = input.UserId,
            WeekStart = input.WeekStart,
            WeekEnd = input.WeekEnd,
            Status = input.Status,
            ReasonCode = input.ReasonCode,
            TaskCountTotal = input.TaskCountTotal,
            TaskCountStudy = input.TaskCountStudy,
            TaskCountReview = input.TaskCountReview,
            TaskCountDiagnostic = input.TaskCountDiagnostic,
            PreservedTaskCount = input.PreservedTaskCount,
            RecommendationCandidateCount = input.RecommendationCandidateCount,
            RecommendationScheduledCount = input.RecommendationScheduledCount,
            RecommendationSkippedByCapacityCount = input.RecommendationSkippedByCapacityCount,
            RecommendationSkippedByDuplicateCount = input.RecommendationSkippedByDuplicateCount,
            TopRecommendationPriorityScore = input.TopRecommendationPriorityScore,
            DailyCapacity = input.DailyCapacity,
            WorkingDaily = input.WorkingDaily,
            BufferDaily = input.BufferDaily,
            EffectiveCapacityMultiplier = input.EffectiveCapacityMultiplier,
            DynamicBufferRate = input.DynamicBufferRate,
            PriorityActiveCount = input.PriorityActiveCount,
            PriorityPlacedCount = input.PriorityPlacedCount,
            PrioritySkippedTopicIds = input.PrioritySkippedTopicIds,
            InjectedReviewTaskCount = input.InjectedReviewTaskCount,
            PerDayRemaining = input.PerDayRemaining,
            QualityScore = input.QualityScore,
            CorrelationId = input.CorrelationId,
            IdempotencyKey = input.IdempotencyKey,
            DurationMs = input.DurationMs,
            BreakdownJson = json
        };
    }
}
