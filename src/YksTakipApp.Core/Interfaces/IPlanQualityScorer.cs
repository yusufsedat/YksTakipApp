using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

/// <summary>
/// Generation-time PlanQualityScore üretir. Sadece Success yolu için çağrılır.
/// Tüm component'lerde "yüksek = iyi". Risk metrikleri "Safety" olarak adlandırıldı.
/// </summary>
public interface IPlanQualityScorer
{
    PlanQualityScore Score(PlanScoringInput input);
}

public sealed class PlanScoringInput
{
    public int WorkingDaily { get; init; }

    /// <summary>Saved task'lar (Plan'ın tüm tip kayıtları); subject dağılımı için kullanılır.</summary>
    public IReadOnlyList<ScheduledTaskSnapshot> Tasks { get; init; } = Array.Empty<ScheduledTaskSnapshot>();

    public IReadOnlyList<int> PerDayRemaining { get; init; } = Array.Empty<int>();

    public int PriorityActiveCount { get; init; }
    public int PriorityPlacedCount { get; init; }
    public int RecommendationCandidateCount { get; init; }
    public int RecommendationScheduledCount { get; init; }
}

public sealed record ScheduledTaskSnapshot(int TopicId, string Subject, DateOnly TaskDate, int DurationMinutes);
