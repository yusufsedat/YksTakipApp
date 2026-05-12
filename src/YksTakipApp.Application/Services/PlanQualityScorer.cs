using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Application.Services;

/// <summary>
/// Saf scorer. Hiçbir IO yok; deterministic.
/// </summary>
public sealed class PlanQualityScorer : IPlanQualityScorer
{
    /// <summary>Healthy >= 60, Warning 40-59, Risky < 40.</summary>
    public PlanQualityScore Score(PlanScoringInput input)
    {
        var capacityFit = ComputeCapacityFit(input);
        var priorityCoverage = ComputePriorityCoverage(input);
        var weaknessCoverage = ComputeWeaknessCoverage(input);
        var subjectBalance = ComputeSubjectBalance(input);
        var repetitionSafety = ComputeRepetitionSafety(input);
        var overloadSafety = ComputeOverloadSafety(input);

        var total = (int)Math.Round(
            (capacityFit + priorityCoverage + weaknessCoverage + subjectBalance + repetitionSafety + overloadSafety) / 6.0,
            MidpointRounding.AwayFromZero);
        total = Clamp(total);

        var band = total >= 60 ? PlanQualityBand.Healthy
                  : total >= 40 ? PlanQualityBand.Warning
                  : PlanQualityBand.Risky;

        return new PlanQualityScore(
            Total: total,
            Band: band,
            CapacityFit: capacityFit,
            PriorityCoverage: priorityCoverage,
            WeaknessCoverage: weaknessCoverage,
            SubjectBalance: subjectBalance,
            RepetitionSafety: repetitionSafety,
            OverloadSafety: overloadSafety);
    }

    /// <summary>Hedef doluluk %85; mesafe arttıkça skor düşer. workingDaily=0 ise nötr 50.</summary>
    private static int ComputeCapacityFit(PlanScoringInput input)
    {
        if (input.WorkingDaily <= 0 || input.PerDayRemaining.Count == 0)
            return 50;
        var totalCapacity = (double)input.WorkingDaily * input.PerDayRemaining.Count;
        if (totalCapacity <= 0)
            return 50;
        var remaining = input.PerDayRemaining.Sum();
        var used = totalCapacity - remaining;
        var usedRatio = Math.Clamp(used / totalCapacity, 0.0, 1.0);
        const double target = 0.85;
        var distance = Math.Abs(usedRatio - target);
        // distance 0 -> 100, distance 0.85 -> 0
        var score = 100.0 * (1.0 - distance / target);
        return Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero));
    }

    private static int ComputePriorityCoverage(PlanScoringInput input)
    {
        if (input.PriorityActiveCount <= 0)
            return 100;
        var ratio = input.PriorityPlacedCount / (double)input.PriorityActiveCount;
        return Clamp((int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero));
    }

    private static int ComputeWeaknessCoverage(PlanScoringInput input)
    {
        if (input.RecommendationCandidateCount <= 0)
            return 100;
        var ratio = input.RecommendationScheduledCount / (double)input.RecommendationCandidateCount;
        return Clamp((int)Math.Round(ratio * 100, MidpointRounding.AwayFromZero));
    }

    /// <summary>HHI tabanlı çeşitlilik. Tek subject -> 0; eşit dağılım -> ~100.</summary>
    private static int ComputeSubjectBalance(PlanScoringInput input)
    {
        if (input.Tasks.Count == 0)
            return 50;
        var bySubject = input.Tasks
            .GroupBy(t => t.Subject ?? "")
            .Select(g => (double)g.Sum(t => t.DurationMinutes))
            .ToList();
        var total = bySubject.Sum();
        if (total <= 0)
            return 50;
        // HHI: sum of squared shares. 1/N -> minimum, 1 -> max.
        var hhi = bySubject.Sum(s => (s / total) * (s / total));
        // Normalize: HHI=1 -> 0, HHI=1/N -> 100.
        var n = bySubject.Count;
        if (n <= 1)
            return 0;
        var minHhi = 1.0 / n;
        var normalized = (1.0 - hhi) / (1.0 - minHhi);
        return Clamp((int)Math.Round(normalized * 100, MidpointRounding.AwayFromZero));
    }

    /// <summary>Aynı topic'in aynı günde tekrarı veya 1 günden az ara penalize edilir.</summary>
    private static int ComputeRepetitionSafety(PlanScoringInput input)
    {
        if (input.Tasks.Count <= 1)
            return 100;
        var penalty = 0;
        var byTopic = input.Tasks.GroupBy(t => t.TopicId);
        foreach (var grp in byTopic)
        {
            var dates = grp.Select(t => t.TaskDate).OrderBy(d => d).ToList();
            for (var i = 1; i < dates.Count; i++)
            {
                var diff = dates[i].DayNumber - dates[i - 1].DayNumber;
                if (diff <= 0)
                    penalty += 25;
                else if (diff < 2)
                    penalty += 10;
            }
        }
        return Clamp(100 - penalty);
    }

    /// <summary>Tek günde kapasitenin %100'ünü aşan task toplamı penalize edilir.</summary>
    private static int ComputeOverloadSafety(PlanScoringInput input)
    {
        if (input.WorkingDaily <= 0 || input.PerDayRemaining.Count == 0)
            return 100;
        var totalCapacity = input.WorkingDaily;
        var penalty = 0;
        foreach (var remaining in input.PerDayRemaining)
        {
            var used = totalCapacity - remaining;
            if (used <= totalCapacity)
                continue;
            var overflowRatio = (used - totalCapacity) / (double)totalCapacity;
            penalty += (int)Math.Round(overflowRatio * 100, MidpointRounding.AwayFromZero);
        }
        return Clamp(100 - penalty);
    }

    private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
}
