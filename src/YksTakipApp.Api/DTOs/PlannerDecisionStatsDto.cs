using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Api.DTOs;

/// <summary>
/// Beta öncesi/sırası operasyonel kontrol için tek bakışta özet. Faz 7 hazırlık kontrol listesi
/// (en sık reasonCode, quality avg/band dağılımı, priority placement rate) buradan okunur.
/// </summary>
public sealed class PlannerDecisionStatsDto
{
    public DateTime? WindowFromUtc { get; init; }
    public DateTime? WindowToUtc { get; init; }
    public int TotalCalls { get; init; }
    public int SuccessCount { get; init; }
    public int NoPlanCount { get; init; }

    public IReadOnlyList<NoPlanReasonStatDto> TopNoPlanReasons { get; init; } = Array.Empty<NoPlanReasonStatDto>();

    public double? AvgQualityScore { get; init; }
    public QualityBandDistributionDto QualityBandDistribution { get; init; } = new();

    /// <summary>
    /// Success satırlarında placed/active oranlarının ortalaması. activeCount=0 satırları "nötr"
    /// kabul edilir (1.0 katkı vermez, hesaba katılmaz).
    /// </summary>
    public double? PriorityFulfillmentRate { get; init; }

    /// <summary>activeCount > placedCount olan success satır sayısı.</summary>
    public int CallsWithUnplacedPriority { get; init; }
}

public sealed record NoPlanReasonStatDto(PlanGenerationReasonCode ReasonCode, int Count);

public sealed class QualityBandDistributionDto
{
    public int Healthy { get; init; }
    public int Warning { get; init; }
    public int Risky { get; init; }
}
