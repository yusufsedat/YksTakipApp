using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public sealed class ContextualInteractionPhaseTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Explainability_ShouldReturnExpectedReasonCode()
    {
        await using var db = CreateDb();
        db.Topics.Add(new Topic { Id = 1, Name = "Paragraf", Subject = "Turkce", Category = "TYT", OsymWeight = 1.4 });
        db.UserTopics.Add(new UserTopic { UserId = 7, TopicId = 1, IsLocked = true, MasteryStatus = MasteryStatus.NeedsReview });
        db.ExamResults.Add(new ExamResult { UserId = 7, ExamName = "d", Date = DateTime.UtcNow, ExamType = "TYT", NetTyt = 20, NetAyt = 0 });
        db.ExamDetails.Add(new ExamDetail { ExamResultId = 1, Subject = "Turkce", Correct = 0, Wrong = 8, Blank = 2 });
        await db.SaveChangesAsync();

        var svc = new RecommendationService(db, NullLogger<RecommendationService>.Instance);
        var result = await svc.GetDailyRecommendationsAsync(7, CancellationToken.None);
        result.Should().NotBeEmpty();
        result[0].ReasonCode.Should().Be(RecommendationReasonCode.WeakExamTrend);
        result[0].ReasonShort.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task NotificationPolicy_ApproachingTarget_ShouldEmitSingleMessagePerDay()
    {
        await using var db = CreateDb();
        var goalId = Guid.NewGuid();
        db.Users.Add(new User { Id = 1, Name = "u", Email = "n@test", PasswordHash = "x", ActiveGoalVersionId = goalId });
        db.UserGoals.Add(new UserGoal { Id = goalId, UserId = 1, TargetUniversity = "x", TargetDepartment = "x", DailyAvailableMinutes = 120, CreatedAt = DateTime.UtcNow });
        var day = DateOnly.FromDateTime(DateTime.UtcNow);
        db.StudyTimes.Add(new StudyTime { UserId = 1, Date = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(8), DurationMinutes = 90 });
        await db.SaveChangesAsync();

        var svc = new NotificationPolicyService(db);
        var first = await svc.PreviewDailyNotificationsAsync(1, day);
        first.Any(x => x.Type == "capacity_close").Should().BeTrue();

        db.UserNotificationLogs.Add(new UserNotificationLog { UserId = 1, NotificationType = "capacity_close", Message = "m", PayloadJson = "{}", TargetDate = day });
        await db.SaveChangesAsync();

        var second = await svc.PreviewDailyNotificationsAsync(1, day);
        second.Any(x => x.Type == "capacity_close").Should().BeFalse();
    }

    [Fact]
    public async Task CommandExecution_SameKey_ShouldReplayWithoutDuplicate()
    {
        await using var db = CreateDb();
        var svc = new CommandExecutionService(db);
        var first = await svc.AcquireAsync(3, "planner.generate", "key-1", CancellationToken.None);
        first.ShouldExecute.Should().BeTrue();
        await svc.CompleteAsync(first.Execution.Id, "{\"ok\":true}", CancellationToken.None);

        var second = await svc.AcquireAsync(3, "planner.generate", "key-1", CancellationToken.None);
        second.ShouldExecute.Should().BeFalse();
        second.IsReplay.Should().BeTrue();
        second.Execution.Status.Should().Be(CommandExecutionStatus.Completed);
    }
}
