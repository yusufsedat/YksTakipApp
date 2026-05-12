using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YksTakipApp.Application.Options;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public sealed class PlannerAdaptationPhaseTests
{
    private sealed class FakeRecommendationService : IRecommendationService
    {
        private readonly List<TopicPriorityDto> _items;
        public FakeRecommendationService(List<TopicPriorityDto> items) => _items = items;
        public Task<List<TopicPriorityDto>> GetDailyRecommendationsAsync(int userId, CancellationToken ct) => Task.FromResult(_items);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void DynamicBuffer_SuccessfulUser_ShouldBeLower()
    {
        var high = DynamicPlannerService.CalculateDynamicBufferRate(1.1);
        var low = DynamicPlannerService.CalculateDynamicBufferRate(0.6);
        high.Should().BeLessThan(low);
        high.Should().BeInRange(0.10, 0.35);
        low.Should().BeInRange(0.10, 0.35);
    }

    [Fact]
    public void CapacityMultiplier_SlowerUser_ShouldDecrease()
    {
        DynamicPlannerService.CalculateCapacityMultiplier(0.8).Should().Be(0.9);
        DynamicPlannerService.CalculateCapacityMultiplier(1.1).Should().Be(1.08);
    }

    [Fact]
    public async Task IncrementalPlanning_ShouldKeepPastCompleted_AndReviseFuture()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-1);

        var goalId = Guid.NewGuid();
        db.Users.Add(new User { Id = 1, Name = "u", Email = "u@test", PasswordHash = "x", ActiveGoalVersionId = goalId });
        db.UserGoals.Add(new UserGoal { Id = goalId, UserId = 1, TargetUniversity = "x", TargetDepartment = "x", DailyAvailableMinutes = 180, CreatedAt = DateTime.UtcNow });
        db.Topics.Add(new Topic { Id = 10, Name = "Temel", Subject = "Mat", Category = "TYT" });
        db.Topics.Add(new Topic { Id = 12, Name = "Problemler", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 10, Status = TopicStatus.InProgress });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 12, Status = TopicStatus.InProgress });
        db.ScheduleTasks.Add(new ScheduleTask
        {
            UserId = 1,
            TopicId = 10,
            TaskDate = today.AddDays(-1),
            DurationMinutes = 30,
            Status = ScheduleTaskStatus.Completed,
            TaskType = TaskType.Study
        });
        db.ScheduleTasks.Add(new ScheduleTask
        {
            UserId = 1,
            TopicId = 10,
            TaskDate = today.AddDays(1),
            DurationMinutes = 30,
            Status = ScheduleTaskStatus.Planned,
            TaskType = TaskType.Study
        });
        await db.SaveChangesAsync();

        var planner = new DynamicPlannerService(
            db,
            new FakeRecommendationService([
                new TopicPriorityDto(10, "Temel", "Mat", 80, "r", RecommendationType.TopicStudy, RecommendationReasonCode.LowStudyTime, "short"),
                new TopicPriorityDto(12, "Problemler", "Mat", 70, "r", RecommendationType.TopicStudy, RecommendationReasonCode.LowStudyTime, "short")
            ]),
            new AdaptationService(db, NullLogger<AdaptationService>.Instance, Options.Create(new AdaptationPolicyOptions())),
            new PlannerDecisionContextBuilder(),
            new PlannerDecisionLogger(db, NullLogger<PlannerDecisionLogger>.Instance),
            new PlanQualityScorer(),
            NullLogger<DynamicPlannerService>.Instance);

        var result = await planner.GenerateWeeklyPlanAsync(1, weekStart);
        result.Status.Should().Be(PlanGenerationStatus.Success);
        result.Tasks.Should().Contain(t => t.TaskDate == today.AddDays(-1) && t.Status == ScheduleTaskStatus.Completed);
        result.Tasks.Count(t => t.TaskDate >= today && t.Status == ScheduleTaskStatus.Planned).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PriorityContinuity_TtlExpired_ShouldAutoClose()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var goalId = Guid.NewGuid();
        db.Users.Add(new User { Id = 1, Name = "u", Email = "u2@test", PasswordHash = "x", ActiveGoalVersionId = goalId });
        db.UserGoals.Add(new UserGoal { Id = goalId, UserId = 1, TargetUniversity = "x", TargetDepartment = "x", DailyAvailableMinutes = 150, CreatedAt = DateTime.UtcNow });
        db.Topics.Add(new Topic { Id = 11, Name = "Fonksiyon", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic
        {
            UserId = 1,
            TopicId = 11,
            IsPriorityRequested = true,
            PriorityRequestedAt = DateTime.UtcNow.AddDays(-10),
            PriorityExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var planner = new DynamicPlannerService(
            db,
            new FakeRecommendationService([]),
            new AdaptationService(db, NullLogger<AdaptationService>.Instance, Options.Create(new AdaptationPolicyOptions())),
            new PlannerDecisionContextBuilder(),
            new PlannerDecisionLogger(db, NullLogger<PlannerDecisionLogger>.Instance),
            new PlanQualityScorer(),
            NullLogger<DynamicPlannerService>.Instance);

        await planner.GenerateWeeklyPlanAsync(1, today);
        var ut = await db.UserTopics.SingleAsync(x => x.UserId == 1 && x.TopicId == 11);
        ut.IsPriorityRequested.Should().BeFalse();
        ut.PriorityResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task KumbaraInjection_ShouldCreateReviewTasksWithCap()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var goalId = Guid.NewGuid();
        db.Users.Add(new User { Id = 1, Name = "u", Email = "u3@test", PasswordHash = "x", ActiveGoalVersionId = goalId });
        db.UserGoals.Add(new UserGoal { Id = goalId, UserId = 1, TargetUniversity = "x", TargetDepartment = "x", DailyAvailableMinutes = 240, CreatedAt = DateTime.UtcNow });
        for (var i = 1; i <= 6; i++)
        {
            db.Topics.Add(new Topic { Id = i, Name = $"Topic{i}", Subject = $"Sub{i}", Category = "TYT" });
            db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = i, Status = TopicStatus.InProgress });
            db.ProblemNotes.Add(new ProblemNote { UserId = 1, TagsJson = $"[\"Sub{i}\"]", ImageUrl = "x", SolutionLearned = false, IsDeleted = false });
        }

        await db.SaveChangesAsync();
        var planner = new DynamicPlannerService(
            db,
            new FakeRecommendationService([]),
            new AdaptationService(db, NullLogger<AdaptationService>.Instance, Options.Create(new AdaptationPolicyOptions())),
            new PlannerDecisionContextBuilder(),
            new PlannerDecisionLogger(db, NullLogger<PlannerDecisionLogger>.Instance),
            new PlanQualityScorer(),
            NullLogger<DynamicPlannerService>.Instance);

        var result = await planner.GenerateWeeklyPlanAsync(1, today);
        result.Tasks.Count(t => t.TaskType == TaskType.Review).Should().BeLessOrEqualTo(4);
    }

    [Fact]
    public async Task AdaptationService_LowAndHighPerformance_ShouldAdjustConfidenceFaster()
    {
        await using var db = CreateDb();
        db.Topics.Add(new Topic { Id = 1, Name = "Main", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, MasteryConfidence = 0.8, MasteryStatus = MasteryStatus.Mastered, IsLocked = false });
        await db.SaveChangesAsync();
        var service = new AdaptationService(db, NullLogger<AdaptationService>.Instance, Options.Create(new AdaptationPolicyOptions()));

        await service.EvaluateTopicPerformanceAsync(1, 1, 10);
        var afterLow = await db.UserTopics.SingleAsync(x => x.UserId == 1 && x.TopicId == 1);
        afterLow.MasteryConfidence.Should().BeLessThan(0.8);

        await service.EvaluateTopicPerformanceAsync(1, 1, 90);
        var afterHigh = await db.UserTopics.SingleAsync(x => x.UserId == 1 && x.TopicId == 1);
        afterHigh.MasteryConfidence.Should().BeGreaterOrEqualTo(afterLow.MasteryConfidence);
    }
}
