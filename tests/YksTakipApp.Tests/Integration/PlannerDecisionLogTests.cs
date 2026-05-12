using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// PlannerDecisionLog yazımı: her erken çıkış + Success yolunda 1 satır. Logger SQLite hatasında
/// planner result döner, structured LogError çağrılır, exception sızmaz.
/// </summary>
public sealed class PlannerDecisionLogTests : IAsyncDisposable
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

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static DynamicPlannerService CreatePlanner(
        AppDbContext db,
        IEnumerable<TopicPriorityDto>? recs = null,
        IPlannerDecisionLogger? loggerOverride = null) =>
        new(
            db,
            new FakeRecommendationService(recs?.ToList() ?? new List<TopicPriorityDto>()),
            new AdaptationService(db, NullLogger<AdaptationService>.Instance, Options.Create(new AdaptationPolicyOptions())),
            new PlannerDecisionContextBuilder(),
            loggerOverride ?? new PlannerDecisionLogger(db, NullLogger<PlannerDecisionLogger>.Instance),
            new PlanQualityScorer(),
            NullLogger<DynamicPlannerService>.Instance);

    private static async Task SeedUserWithGoalAsync(AppDbContext db, int userId, int dailyMinutes = 240)
    {
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

    private static async Task SeedUserAsync(AppDbContext db, int userId)
    {
        db.Users.Add(new User { Id = userId, Name = "u", Email = $"u{userId}@t", PasswordHash = "x" });
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in _connections)
            await c.DisposeAsync();
    }

    [Fact]
    public async Task SuccessPath_WritesSingleLogRowWithBreakdown()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 240);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db, new[]
        {
            new TopicPriorityDto(1, "T1", "S1", 80, "r", RecommendationType.TopicStudy, RecommendationReasonCode.LowStudyTime, "short")
        });
        var metadata = new PlannerCallMetadata("corr-1", "idem-1");
        var result = await planner.GenerateWeeklyPlanAsync(1, today, metadata);

        result.Status.Should().Be(PlanGenerationStatus.Success);

        var logs = await db.PlannerDecisionLogs.AsNoTracking().Where(x => x.UserId == 1).ToListAsync();
        logs.Should().HaveCount(1);
        var row = logs[0];
        row.Status.Should().Be(PlanGenerationStatus.Success);
        row.ReasonCode.Should().Be(PlanGenerationReasonCode.None);
        row.TaskCountTotal.Should().BeGreaterThan(0);
        row.CorrelationId.Should().Be("corr-1");
        row.IdempotencyKey.Should().Be("idem-1");
        row.WorkingDaily.Should().BeGreaterThan(0);

        using var json = JsonDocument.Parse(row.BreakdownJson);
        json.RootElement.TryGetProperty("capacity", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("priority", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("recommendationSummary", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("perDayRemaining", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RequiresGoal_WritesLogWithRequiresGoalReason()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserAsync(db, 1);

        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.NoPlanGenerated);
        result.ReasonCode.Should().Be(PlanGenerationReasonCode.RequiresGoal);

        var row = await db.PlannerDecisionLogs.AsNoTracking().SingleAsync();
        row.ReasonCode.Should().Be(PlanGenerationReasonCode.RequiresGoal);
        row.TaskCountTotal.Should().Be(0);
    }

    [Fact]
    public async Task DailyCapacityTooLow_WritesLogWithCapacityFields()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 20);

        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.Status.Should().Be(PlanGenerationStatus.NoPlanGenerated);
        result.ReasonCode.Should().Be(PlanGenerationReasonCode.DailyCapacityTooLow);

        var row = await db.PlannerDecisionLogs.AsNoTracking().SingleAsync();
        row.ReasonCode.Should().Be(PlanGenerationReasonCode.DailyCapacityTooLow);
        row.DailyCapacity.Should().Be(20);
        row.WorkingDaily.Should().BeLessThan(30);
    }

    [Fact]
    public async Task NoTopics_WritesLogWithNoTopicsReason()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 240);

        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.ReasonCode.Should().Be(PlanGenerationReasonCode.NoTopics);
        var row = await db.PlannerDecisionLogs.AsNoTracking().SingleAsync();
        row.ReasonCode.Should().Be(PlanGenerationReasonCode.NoTopics);
    }

    [Fact]
    public async Task NoRecommendations_WritesLogWithNoRecommendationsReason()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 240);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();

        var planner = CreatePlanner(db);
        var result = await planner.GenerateWeeklyPlanAsync(1, today);

        result.ReasonCode.Should().Be(PlanGenerationReasonCode.NoRecommendations);
        var row = await db.PlannerDecisionLogs.AsNoTracking().SingleAsync();
        row.ReasonCode.Should().Be(PlanGenerationReasonCode.NoRecommendations);
    }

    private sealed class CapturingTestLogger : ILogger<PlannerDecisionLogger>
    {
        public List<(LogLevel level, string message, Exception? ex)> Entries { get; } = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task LoggerFailure_PlanStillReturns_LogErrorCalled_NoExceptionLeaks()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedUserWithGoalAsync(db, 1, 240);
        db.Topics.Add(new Topic { Id = 1, Name = "T1", Subject = "S1", Category = "TYT" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 1, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();

        // Failing logger: ayrı bir DbContext üzerine; SQLite connection kapatılarak SaveChanges patlatılır.
        var failingConn = new SqliteConnection("DataSource=:memory:");
        failingConn.Open();
        _connections.Add(failingConn);
        var failingOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(failingConn).Options;
        var failingDb = new AppDbContext(failingOptions);
        failingDb.Database.EnsureCreated();
        await failingConn.CloseAsync();

        var testLogger = new CapturingTestLogger();
        var failingLogger = new PlannerDecisionLogger(failingDb, testLogger);
        var planner = CreatePlanner(db,
            recs: new[]
            {
                new TopicPriorityDto(1, "T1", "S1", 80, "r", RecommendationType.TopicStudy, RecommendationReasonCode.LowStudyTime, "short")
            },
            loggerOverride: failingLogger);

        var act = async () => await planner.GenerateWeeklyPlanAsync(1, today, new PlannerCallMetadata("corr-fail", "idem-fail"));
        var result = await act.Should().NotThrowAsync();
        result.Subject.Status.Should().Be(PlanGenerationStatus.Success);

        testLogger.Entries.Should().Contain(e => e.level == LogLevel.Error
            && e.message.Contains("PlannerDecisionLogger persist failed"));
        var errEntry = testLogger.Entries.First(e => e.level == LogLevel.Error);
        errEntry.message.Should().Contain("corr-fail");
        errEntry.message.Should().Contain("idem-fail");

        await failingDb.DisposeAsync();
    }
}
