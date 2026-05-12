using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Entities;

/// <summary>
/// Planner.GenerateWeeklyPlanAsync çağrısı başına 1 kayıt. Hem başarı (Status=Success) hem
/// NoPlanGenerated yolu loglanır. BreakdownJson sabit 5 bölümlü sınırlı snapshot tutar (DB şişmesin).
/// </summary>
public sealed class PlannerDecisionLog
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateOnly WeekEnd { get; set; }

    public PlanGenerationStatus Status { get; set; }
    public PlanGenerationReasonCode ReasonCode { get; set; }

    public int TaskCountTotal { get; set; }
    public int TaskCountStudy { get; set; }
    public int TaskCountReview { get; set; }
    public int TaskCountDiagnostic { get; set; }
    public int PreservedTaskCount { get; set; }

    public int RecommendationCandidateCount { get; set; }
    public int RecommendationScheduledCount { get; set; }

    /// <summary>Önerilen topic için kapasite kalmadığı için planlanamayan sayısı.</summary>
    public int RecommendationSkippedByCapacityCount { get; set; }

    /// <summary>Önerilen topic zaten priority/preserved/review olarak schedule edildiği için atlanan sayısı.</summary>
    public int RecommendationSkippedByDuplicateCount { get; set; }

    public int DailyCapacity { get; set; }
    public int WorkingDaily { get; set; }
    public int BufferDaily { get; set; }
    public double EffectiveCapacityMultiplier { get; set; }
    public double DynamicBufferRate { get; set; }

    public int PriorityActiveCount { get; set; }
    public int PriorityPlacedCount { get; set; }
    public int InjectedReviewTaskCount { get; set; }

    public int? QualityScore { get; set; }
    public PlanQualityBand? QualityBand { get; set; }

    /// <summary>
    /// Sınırlı snapshot. İçerik 5 sabit bölümle kısıtlı: capacity, priority, recommendationSummary,
    /// perDayRemaining, qualityComponents. Ham rec/job listesi yazılmaz.
    /// </summary>
    public string BreakdownJson { get; set; } = "{}";

    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
