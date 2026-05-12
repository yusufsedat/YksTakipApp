using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Api.DTOs;

public sealed class PlannerDecisionLogSummaryDto
{
    public long Id { get; init; }
    public int UserId { get; init; }
    public DateOnly WeekStart { get; init; }
    public DateOnly WeekEnd { get; init; }
    public PlanGenerationStatus Status { get; init; }
    public PlanGenerationReasonCode ReasonCode { get; init; }
    public int TaskCountTotal { get; init; }
    public int? QualityScore { get; init; }
    public PlanQualityBand? QualityBand { get; init; }
    public int DurationMs { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class PlannerDecisionLogDetailDto
{
    public long Id { get; init; }
    public int UserId { get; init; }
    public DateOnly WeekStart { get; init; }
    public DateOnly WeekEnd { get; init; }
    public PlanGenerationStatus Status { get; init; }
    public PlanGenerationReasonCode ReasonCode { get; init; }
    public int TaskCountTotal { get; init; }
    public int TaskCountStudy { get; init; }
    public int TaskCountReview { get; init; }
    public int TaskCountDiagnostic { get; init; }
    public int PreservedTaskCount { get; init; }
    public int RecommendationCandidateCount { get; init; }
    public int RecommendationScheduledCount { get; init; }
    public int RecommendationSkippedByCapacityCount { get; init; }
    public int RecommendationSkippedByDuplicateCount { get; init; }
    public int DailyCapacity { get; init; }
    public int WorkingDaily { get; init; }
    public int BufferDaily { get; init; }
    public double EffectiveCapacityMultiplier { get; init; }
    public double DynamicBufferRate { get; init; }
    public int PriorityActiveCount { get; init; }
    public int PriorityPlacedCount { get; init; }
    public int InjectedReviewTaskCount { get; init; }
    public int? QualityScore { get; init; }
    public PlanQualityBand? QualityBand { get; init; }
    public string BreakdownJson { get; init; } = "{}";
    public string? CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }
    public int DurationMs { get; init; }
    public DateTime CreatedAt { get; init; }
}
