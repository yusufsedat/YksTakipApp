using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

/// <summary>
/// DynamicPlannerService DecisionLog hazırlığını delege eder. Builder hem context'i hem
/// BreakdownJson'ı üretir. Planner servisi serialization veya alan adlandırma bilmez.
/// </summary>
public interface IPlannerDecisionContextBuilder
{
    /// <summary>
    /// Tüm çağrı türleri (Success + erken NoPlanGenerated yolları) için tek API.
    /// Sayıların hepsi opsiyonel default'larla geçer; çağıran sadece bildiği alanları doldurur.
    /// </summary>
    PlannerDecisionContext Build(PlannerDecisionInput input);
}

public sealed class PlannerDecisionInput
{
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
    public int TopRecommendationPriorityScore { get; init; }

    public int DailyCapacity { get; init; }
    public int WorkingDaily { get; init; }
    public int BufferDaily { get; init; }
    public double EffectiveCapacityMultiplier { get; init; } = 1.0;
    public double DynamicBufferRate { get; init; }

    public int PriorityActiveCount { get; init; }
    public int PriorityPlacedCount { get; init; }
    public IReadOnlyList<int> PrioritySkippedTopicIds { get; init; } = Array.Empty<int>();
    public int InjectedReviewTaskCount { get; init; }

    public IReadOnlyList<int> PerDayRemaining { get; init; } = Array.Empty<int>();

    public PlanQualityScore? QualityScore { get; init; }

    public string? CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }
    public int DurationMs { get; init; }
}
