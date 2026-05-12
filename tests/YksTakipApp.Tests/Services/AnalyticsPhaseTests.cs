using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public sealed class AnalyticsPhaseTests
{
    private sealed class EmptyRecommendationService : IRecommendationService
    {
        public Task<List<TopicPriorityDto>> GetDailyRecommendationsAsync(int userId, CancellationToken ct) => Task.FromResult(new List<TopicPriorityDto>());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ChurnEvent_ShouldBeStored_AndDuplicatePrevented()
    {
        await using var db = CreateDb();
        db.Users.Add(new User { Id = 1, Name = "u", Email = "a@a.com", PasswordHash = "x" });
        await db.SaveChangesAsync();
        var planner = new DynamicPlannerService(
            db,
            new EmptyRecommendationService(),
            new AdaptationService(db, NullLogger<AdaptationService>.Instance, Microsoft.Extensions.Options.Options.Create(new YksTakipApp.Application.Options.AdaptationPolicyOptions())),
            new PlannerDecisionContextBuilder(),
            new PlannerDecisionLogger(db, NullLogger<PlannerDecisionLogger>.Instance),
            new PlanQualityScorer(),
            NullLogger<DynamicPlannerService>.Instance);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await planner.CheckAndTriggerChurnAsync(1, today.AddDays(-1), today.AddDays(5));
        await planner.CheckAndTriggerChurnAsync(1, today.AddDays(-1), today.AddDays(5));

        var events = await db.UserPlannerChurnEvents.ToListAsync();
        events.Count(e => e.ReasonCode == PlannerChurnReasonCode.NoPlannedToday).Should().Be(1);
        events.Count(e => e.ReasonCode == PlannerChurnReasonCode.NoStudyTaskInWeek).Should().Be(1);
    }

    [Fact]
    public async Task ChurnSummary_ShouldAggregateCorrectly()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Users.Add(new User { Id = 1, Name = "u1", Email = "u1@test", PasswordHash = "x", CreatedAt = DateTime.UtcNow.AddDays(-3) });
        db.Users.Add(new User { Id = 2, Name = "u2", Email = "u2@test", PasswordHash = "x", CreatedAt = DateTime.UtcNow.AddDays(-30) });
        db.UserPlannerChurnEvents.AddRange(
            new UserPlannerChurnEvent { UserId = 1, WeekStart = today.AddDays(-7), WeekEnd = today, TriggerDate = today.AddDays(-1), ReasonCode = PlannerChurnReasonCode.NoPlannedToday, DaysSincePlanGenerated = 2 },
            new UserPlannerChurnEvent { UserId = 2, WeekStart = today.AddDays(-7), WeekEnd = today, TriggerDate = today.AddDays(-2), ReasonCode = PlannerChurnReasonCode.NoStudyTaskInWeek, DaysSincePlanGenerated = 4 });
        await db.SaveChangesAsync();
        var analytics = new AnalyticsService(db);

        var summary = await analytics.GetChurnSummaryAsync(today.AddDays(-14), today);
        summary.ChurnTriggerCount.Should().Be(2);
        summary.ChurnedUserCount.Should().Be(2);
        summary.AvgChurnLatencyDays.Should().Be(3);
    }

    [Fact]
    public async Task FeedbackScores_ShouldReflectDeferralAndCompletion()
    {
        await using var db = CreateDb();
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-6));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        db.Users.Add(new User { Id = 7, Name = "u7", Email = "u7@test", PasswordHash = "x", CreatedAt = DateTime.UtcNow.AddDays(-20) });
        db.Users.Add(new User { Id = 8, Name = "u8", Email = "u8@test", PasswordHash = "x", CreatedAt = DateTime.UtcNow.AddDays(-20) });
        db.ScheduleTasks.AddRange(
            new ScheduleTask { UserId = 7, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Deferred, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 7, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Skipped, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 7, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Deferred, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 7, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Skipped, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 7, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Completed, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 8, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Completed, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 8, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Completed, TaskType = TaskType.Study },
            new ScheduleTask { UserId = 8, TopicId = 1, TaskDate = to, DurationMinutes = 30, Status = ScheduleTaskStatus.Completed, TaskType = TaskType.Study });
        await db.SaveChangesAsync();
        var analytics = new AnalyticsService(db);

        var struggling = await analytics.GetFeedbackForUserAsync(7, from, to);
        var healthy = await analytics.GetFeedbackForUserAsync(8, from, to);
        struggling.DifficultyScore.Should().BeGreaterThan(healthy.DifficultyScore);
        struggling.SatisfactionScore.Should().BeLessThan(healthy.SatisfactionScore);
    }
}
