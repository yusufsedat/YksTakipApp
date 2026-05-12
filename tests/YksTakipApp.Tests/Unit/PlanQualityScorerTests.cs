using FluentAssertions;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Tests.Unit;

public sealed class PlanQualityScorerTests
{
    private static readonly PlanQualityScorer Sut = new();

    private static ScheduledTaskSnapshot T(int topicId, string subject, DateOnly date, int duration = 60)
        => new(topicId, subject, date, duration);

    [Fact]
    public void EmptyTasks_NoCapacityUsed_GivesLowCapacityFit_ButHealthyOthers()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 180, 180, 180, 180, 180, 180, 180 },
            Tasks = Array.Empty<ScheduledTaskSnapshot>(),
            PriorityActiveCount = 0,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.CapacityFit.Should().BeLessThan(10);
        score.PriorityCoverage.Should().Be(100);
        score.WeaknessCoverage.Should().Be(100);
    }

    [Fact]
    public void FullPriorityCoverage_GivesPriority100()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 90, 90, 90, 90, 90, 90, 90 },
            Tasks = new[] { T(1, "Mat", today) },
            PriorityActiveCount = 3,
            PriorityPlacedCount = 3,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.PriorityCoverage.Should().Be(100);
    }

    [Fact]
    public void HalfPriorityCoverage_GivesPriority50()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 90, 90, 90, 90, 90, 90, 90 },
            Tasks = new[] { T(1, "Mat", today) },
            PriorityActiveCount = 4,
            PriorityPlacedCount = 2,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.PriorityCoverage.Should().Be(50);
    }

    [Fact]
    public void SingleSubject_ReducesSubjectBalance()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tasks = Enumerable.Range(0, 6).Select(i => T(i + 1, "Mat", today.AddDays(i))).ToArray();
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 60, 60, 60, 60, 60, 60, 60 },
            Tasks = tasks,
            PriorityActiveCount = 0,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.SubjectBalance.Should().BeLessThan(30);
    }

    [Fact]
    public void DiverseSubjects_GivesHighSubjectBalance()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tasks = new[]
        {
            T(1, "Mat", today),
            T(2, "Fiz", today.AddDays(1)),
            T(3, "Kim", today.AddDays(2)),
            T(4, "Bio", today.AddDays(3))
        };
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 120, 120, 120, 120, 180, 180, 180 },
            Tasks = tasks,
            PriorityActiveCount = 0,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.SubjectBalance.Should().BeGreaterThan(70);
    }

    [Fact]
    public void SameTopicSameDay_LowersRepetitionSafety()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tasks = new[]
        {
            T(1, "Mat", today),
            T(1, "Mat", today),
            T(1, "Mat", today)
        };
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 0, 180, 180, 180, 180, 180, 180 },
            Tasks = tasks,
            PriorityActiveCount = 0,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.RepetitionSafety.Should().BeLessThan(80);
    }

    [Fact]
    public void NoOverload_GivesOverloadSafety100()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 10, 20, 30, 40, 50, 60, 70 },
            Tasks = new[] { T(1, "Mat", today) },
            PriorityActiveCount = 0,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.OverloadSafety.Should().Be(100);
    }

    [Fact]
    public void HeavyOverload_LowersOverloadSafety()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var score = Sut.Score(new PlanScoringInput
        {
            // 1 günde -180 (yani used = 180 - (-180) = 360 > 180 cap).
            WorkingDaily = 180,
            PerDayRemaining = new[] { -180, 180, 180, 180, 180, 180, 180 },
            Tasks = new[] { T(1, "Mat", today) },
            PriorityActiveCount = 0,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 0,
            RecommendationScheduledCount = 0
        });
        score.OverloadSafety.Should().BeLessThan(20);
    }

    [Fact]
    public void HealthyBand_TotalAbove60_GivesHealthy()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tasks = new[]
        {
            T(1, "Mat", today),
            T(2, "Fiz", today.AddDays(1)),
            T(3, "Kim", today.AddDays(2)),
            T(4, "Bio", today.AddDays(3))
        };
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 100,
            // Doluluk ~85% target'a yakın.
            PerDayRemaining = new[] { 15, 15, 15, 15, 15, 15, 15 },
            Tasks = tasks,
            PriorityActiveCount = 2,
            PriorityPlacedCount = 2,
            RecommendationCandidateCount = 3,
            RecommendationScheduledCount = 3
        });
        score.Band.Should().Be(PlanQualityBand.Healthy);
        score.Total.Should().BeGreaterOrEqualTo(60);
    }

    [Fact]
    public void RiskyBand_TotalBelow40_GivesRisky()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var score = Sut.Score(new PlanScoringInput
        {
            WorkingDaily = 180,
            PerDayRemaining = new[] { 180, 180, 180, 180, 180, 180, 180 },
            Tasks = new[] { T(1, "Mat", today), T(1, "Mat", today), T(1, "Mat", today) },
            PriorityActiveCount = 4,
            PriorityPlacedCount = 0,
            RecommendationCandidateCount = 5,
            RecommendationScheduledCount = 0
        });
        score.Band.Should().Be(PlanQualityBand.Risky);
    }
}
