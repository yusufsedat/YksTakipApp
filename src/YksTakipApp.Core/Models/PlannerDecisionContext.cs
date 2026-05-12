using YksTakipApp.Core.Enums;

namespace YksTakipApp.Core.Models;

/// <summary>
/// Endpoint katmanından planner'a taşınan request metadatası. CorrelationId/IdempotencyKey
/// DecisionLog satırına yazılır; null gelmesi sorun değildir.
/// </summary>
public sealed record PlannerCallMetadata(string? CorrelationId, string? IdempotencyKey);

/// <summary>
/// DynamicPlannerService -> PlannerDecisionLogger arasındaki taşıma DTO'su.
/// Planner ham sayıları/dataları builder'a verir; builder bu context'i + BreakdownJson'ı üretir.
/// Servis JSON serialization veya alan adlandırma detayı görmez.
/// </summary>
public sealed class PlannerDecisionContext
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
    public double EffectiveCapacityMultiplier { get; init; }
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
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Builder dolduruyor; planner servisi bu alana dokunmaz.</summary>
    public string BreakdownJson { get; init; } = "{}";
}

/// <summary>
/// PlanQualityScore Faz 6.2'de devreye giriyor. Faz 6.1'de bu tip null olabilir.
/// Tüm component'lerde "yüksek = iyi". Risk metrikleri "Safety" olarak adlandırıldı.
/// </summary>
public sealed record PlanQualityScore(
    int Total,
    PlanQualityBand Band,
    int CapacityFit,
    int PriorityCoverage,
    int WeaknessCoverage,
    int SubjectBalance,
    int RepetitionSafety,
    int OverloadSafety);
