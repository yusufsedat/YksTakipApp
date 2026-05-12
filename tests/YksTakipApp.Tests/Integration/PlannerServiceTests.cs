using FluentAssertions;
using Microsoft.Data.Sqlite;
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

namespace YksTakipApp.Tests.Integration;

/// <summary>
/// DynamicPlannerService iş kuralı testleri (HTTP yok). SQLite in-memory provider üzerinde
/// gerçek EF davranışı çalışır; ExecuteUpdateAsync / ExecuteDeleteAsync gibi koşullar bu sayede
/// kapsanır. Tek tek SqliteConnection açılarak test izolasyonu sağlanır.
/// </summary>
public sealed class PlannerServiceTests : IAsyncDisposable
{
    private readonly List<SqliteConnection> _connections = new();

    private sealed class FakeRecommendationService : IRecommendationService
    {
        private readonly List<TopicPriorityDto> _items;
        public FakeRecommendationService(List<TopicPriorityDto> items) => _items = items;
        public Task<List<TopicPriorityDto>> GetDailyRecommendationsAsync(int userId, CancellationToken ct) =>
            Task.FromResult(_items);
    }

    private AppDbContext CreateDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        _connections.Add(conn);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static DynamicPlannerService CreatePlanner(AppDbContext db, IEnumerable<TopicPriorityDto>? recs = null) =>
        new(
            db,
            new FakeRecommendationService(recs?.ToList() ?? new List<TopicPriorityDto>()),
            new AdaptationService(db, NullLogger<AdaptationService>.Instance, Options.Create(new AdaptationPolicyOptions())),
            new PlannerDecisionContextBuilder(),
            new PlannerDecisionLogger(db, NullLogger<PlannerDecisionLogger>.Instance),
            new PlanQualityScorer(),
            NullLogger<DynamicPlannerService>.Instance);

    private static async Task SeedUserWithGoalAsync(AppDbContext db, int userId, int dailyMinutes = 240)
    {
        // SQLite FK kısıtlamaları aktif: User <-> UserGoal arası döngüsel FK için iki adımda yaz.
        var user = new User { Id = userId, Name = "u", Email = $"u{userId}@t", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var goalId = Guid.NewGuid();
        db.UserGoals.Add(new UserGoal
        {
            Id = goalId,
            UserId = userId,
            TargetUniversity = "x",
            TargetDepartment = "x",
            DailyAvailableMinutes = dailyMinutes,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        user.ActiveGoalVersionId = goalId;
        await db.SaveChangesAsync();
    }

    private static TopicPriorityDto Rec(int topicId, int score = 80) =>
        new(topicId, $"T{topicId}", $"S{topicId}", score, "r", RecommendationType.TopicStudy, RecommendationReasonCode.LowStudyTime, "short");

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _connections)
            await c.DisposeAsync();
    }

    [Fact]
    public async Task GenerateWeeklyPlan_PlacesPriorityTodayOrTomorrow()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 180);
        db.Topics.Add(new Topic { Id = 5, Name = "Limit", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic
        {
            UserId = 1,
            TopicId = 5,
            IsPriorityRequested = true,
            PriorityRequestedAt = DateTime.UtcNow,
            PriorityExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.Success);
        var priorityTask = result.Tasks.FirstOrDefault(t => t.TopicId == 5);
        priorityTask.Should().NotBeNull();
        priorityTask!.TaskDate.Should().BeOneOf(today, today.AddDays(1));
    }

    [Fact]
    public async Task GenerateWeeklyPlan_DoesNotExceedCapacity()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        const int dailyMinutes = 240;
        await SeedUserWithGoalAsync(db, 1, dailyMinutes);
        for (var i = 1; i <= 8; i++)
        {
            db.Topics.Add(new Topic { Id = i, Name = $"T{i}", Subject = $"S{i}", Category = "TYT" });
            db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = i, Status = TopicStatus.InProgress });
        }
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db, Enumerable.Range(1, 8).Select(i => Rec(i, 80)));
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.Success);
        // workingDaily <= floor(240 * (1 - 0.10)) = 216 üst sınır.
        const int workingDailyUpperBound = 216;
        var grouped = result.Tasks
            .Where(t => t.Status == ScheduleTaskStatus.Planned)
            .GroupBy(t => t.TaskDate)
            .Select(g => g.Sum(t => t.DurationMinutes));
        grouped.Should().OnlyContain(sum => sum <= workingDailyUpperBound);
    }

    [Fact]
    public async Task GenerateWeeklyPlan_PreservesCompletedTasks()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 180);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        db.ScheduleTasks.Add(new ScheduleTask
        {
            UserId = 1,
            TopicId = 1,
            TaskDate = today,
            DurationMinutes = 30,
            Status = ScheduleTaskStatus.Completed,
            TaskType = TaskType.Study
        });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db, new[] { Rec(1) });
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.Success);
        result.Tasks.Should().Contain(t => t.TaskDate == today && t.Status == ScheduleTaskStatus.Completed);
    }

    [Fact]
    public async Task GenerateWeeklyPlan_DoesNotModifyPastTasks()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-2);
        await SeedUserWithGoalAsync(db, 1, 180);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        db.ScheduleTasks.Add(new ScheduleTask
        {
            UserId = 1,
            TopicId = 1,
            TaskDate = weekStart,
            DurationMinutes = 45,
            Status = ScheduleTaskStatus.Planned,
            TaskType = TaskType.Study
        });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db, new[] { Rec(1) });
        await planner.GenerateWeeklyPlanAsync(1, weekStart);

        var pastTask = await db.ScheduleTasks
            .AsNoTracking()
            .SingleAsync(t => t.UserId == 1 && t.TaskDate == weekStart);
        pastTask.Status.Should().Be(ScheduleTaskStatus.Planned);
        pastTask.DurationMinutes.Should().Be(45);
    }

    [Fact]
    public async Task GenerateWeeklyPlan_IgnoresExpiredPriority()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 180);
        db.Topics.Add(new Topic { Id = 7, Name = "Eski", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic
        {
            UserId = 1,
            TopicId = 7,
            IsPriorityRequested = true,
            PriorityRequestedAt = DateTime.UtcNow.AddDays(-10),
            PriorityExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db);
        await planner.GenerateWeeklyPlanAsync(1, today);

        var ut = await db.UserTopics.AsNoTracking().SingleAsync(x => x.UserId == 1 && x.TopicId == 7);
        ut.IsPriorityRequested.Should().BeFalse();
        ut.PriorityResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateWeeklyPlan_DoesNotDuplicateTopics()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 240);
        db.Topics.Add(new Topic { Id = 3, Name = "Çift", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic
        {
            UserId = 1,
            TopicId = 3,
            IsPriorityRequested = true,
            PriorityRequestedAt = DateTime.UtcNow,
            PriorityExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db, new[] { Rec(3, 90), Rec(3, 70) });
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.Success);
        result.Tasks.Count(t => t.TopicId == 3 && t.TaskType == TaskType.Study).Should().Be(1);
    }

    [Fact]
    public async Task GenerateWeeklyPlan_WhenNoUserTopics_ReturnsNoTopics()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 180);

        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.NoPlanGenerated);
        result.ReasonCode.Should().Be(PlanGenerationReasonCode.NoTopics);
        result.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWeeklyPlan_WhenTopicsExistButNoRecommendations_ReturnsNoRecommendations()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 180);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();

        // Boş öneri listesi + priority yok + problem-note yok -> NoRecommendations.
        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.NoPlanGenerated);
        result.ReasonCode.Should().Be(PlanGenerationReasonCode.NoRecommendations);
        result.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWeeklyPlan_ReturnsNoPlan_WhenCapacityTooLow()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // DailyAvailableMinutes=20 -> workingDaily=floor(max(30,20)*0.8)=24<30 -> DailyCapacityTooLow.
        await SeedUserWithGoalAsync(db, 1, 20);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db, new[] { Rec(1) });
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.NoPlanGenerated);
        result.ReasonCode.Should().Be(PlanGenerationReasonCode.DailyCapacityTooLow);
        result.MinimumRequiredMinutes.Should().Be(30);
        result.CurrentMinutes.Should().BeLessThan(30);
        result.Tasks.Should().BeEmpty();
    }

    /// <summary>
    /// PATCH-status Completed kolu InMemory provider'da çalışmaz; SQLite ile UpdateStatusAsync
    /// içindeki ExecuteUpdateAsync gerçek EF davranışıyla doğrulanıyor.
    /// </summary>
    [Fact]
    public async Task UpdateStatus_WhenCompleted_ResolvesPriorityRequest()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 180);
        db.Topics.Add(new Topic { Id = 9, Name = "Trigon", Subject = "Mat", Category = "TYT" });
        db.UserTopics.Add(new UserTopic
        {
            UserId = 1,
            TopicId = 9,
            IsPriorityRequested = true,
            PriorityRequestedAt = DateTime.UtcNow,
            PriorityExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        var task = new ScheduleTask
        {
            UserId = 1,
            TopicId = 9,
            TaskDate = today,
            DurationMinutes = 30,
            Status = ScheduleTaskStatus.Planned,
            TaskType = TaskType.Study
        };
        db.ScheduleTasks.Add(task);
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db);
        var updated = await planner.UpdateStatusAsync(1, task.Id, ScheduleTaskStatus.Completed);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(ScheduleTaskStatus.Completed);

        var ut = await db.UserTopics.AsNoTracking().SingleAsync(x => x.UserId == 1 && x.TopicId == 9);
        ut.IsPriorityRequested.Should().BeFalse();
        ut.PriorityResolvedAt.Should().NotBeNull();
    }
}
