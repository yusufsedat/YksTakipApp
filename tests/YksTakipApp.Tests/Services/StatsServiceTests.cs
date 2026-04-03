using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public class StatsServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenUserHasData_ReturnsCorrectSummary()
    {
        var userId = 1;
        await using var db = CreateDb();
        db.ExamResults.AddRange(
            new ExamResult
            {
                UserId = userId,
                ExamName = "A",
                Date = DateTime.UtcNow,
                ExamType = "TYT",
                NetTyt = 80,
                NetAyt = 75,
            },
            new ExamResult
            {
                UserId = userId,
                ExamName = "B",
                Date = DateTime.UtcNow,
                ExamType = "TYT",
                NetTyt = 85,
                NetAyt = 80,
            });
        await db.SaveChangesAsync();

        var studyMock = new Mock<IRepository<StudyTime>>();
        var topicMock = new Mock<IRepository<UserTopic>>();
        studyMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<StudyTime, bool>>>()))
            .ReturnsAsync(new List<StudyTime>
            {
                new() { UserId = userId, DurationMinutes = 60, Date = DateTime.UtcNow.AddDays(-1) },
                new() { UserId = userId, DurationMinutes = 90, Date = DateTime.UtcNow.AddDays(-2) },
            });
        topicMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(new List<UserTopic>
            {
                new() { UserId = userId, TopicId = 1, Status = TopicStatus.Completed },
                new() { UserId = userId, TopicId = 2, Status = TopicStatus.Completed },
            });

        var service = new StatsService(studyMock.Object, topicMock.Object, db);
        var result = await service.GetSummaryAsync(userId);

        result.Should().NotBeNull();
        var resultType = result.GetType();
        resultType.GetProperty("totalMinutesLast7Days")?.GetValue(result).Should().Be(150);
        resultType.GetProperty("completedTopics")?.GetValue(result).Should().Be(2);
        resultType.GetProperty("avgTyt")?.GetValue(result).Should().Be(82.5);
        resultType.GetProperty("avgAyt")?.GetValue(result).Should().Be(77.5);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenUserHasNoData_ReturnsZeroValues()
    {
        var userId = 1;
        await using var db = CreateDb();

        var studyMock = new Mock<IRepository<StudyTime>>();
        var topicMock = new Mock<IRepository<UserTopic>>();
        studyMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<StudyTime, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<StudyTime>());
        topicMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<UserTopic>());

        var service = new StatsService(studyMock.Object, topicMock.Object, db);
        var result = await service.GetSummaryAsync(userId);

        result.Should().NotBeNull();
        var resultType = result.GetType();
        resultType.GetProperty("totalMinutesLast7Days")?.GetValue(result).Should().Be(0);
        resultType.GetProperty("completedTopics")?.GetValue(result).Should().Be(0);
        resultType.GetProperty("avgTyt")?.GetValue(result).Should().Be(0);
        resultType.GetProperty("avgAyt")?.GetValue(result).Should().Be(0);
    }

    [Fact]
    public async Task GetWeeklyAsync_ReturnsWeeklyData()
    {
        var userId = 1;
        await using var db = CreateDb();
        var studyMock = new Mock<IRepository<StudyTime>>();
        var topicMock = new Mock<IRepository<UserTopic>>();
        studyMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<StudyTime, bool>>>()))
            .ReturnsAsync(new List<StudyTime>
            {
                new() { UserId = userId, DurationMinutes = 60, Date = DateTime.UtcNow.Date.AddDays(-1) },
                new() { UserId = userId, DurationMinutes = 90, Date = DateTime.UtcNow.Date.AddDays(-2) },
                new() { UserId = userId, DurationMinutes = 30, Date = DateTime.UtcNow.Date.AddDays(-1) },
            });

        var service = new StatsService(studyMock.Object, topicMock.Object, db);
        var result = await service.GetWeeklyAsync(userId);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IEnumerable<object>>();
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsProgressData()
    {
        var userId = 1;
        var today = DateTime.UtcNow.Date;
        var thisWeekStart = today.AddDays(-6);

        await using var db = CreateDb();
        var studyMock = new Mock<IRepository<StudyTime>>();
        var topicMock = new Mock<IRepository<UserTopic>>();

        var allStudyTimes = new List<StudyTime>
        {
            new() { UserId = userId, DurationMinutes = 100, Date = today.AddDays(-1) },
            new() { UserId = userId, DurationMinutes = 80, Date = today.AddDays(-2) },
            new() { UserId = userId, DurationMinutes = 50, Date = thisWeekStart.AddDays(-2) },
            new() { UserId = userId, DurationMinutes = 30, Date = thisWeekStart.AddDays(-3) },
        };

        var callCount = 0;
        studyMock
            .Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<StudyTime, bool>>>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    return allStudyTimes.Where(st => st.Date >= thisWeekStart && st.Date <= today);
                var lastWeekStart = thisWeekStart.AddDays(-7);
                return allStudyTimes.Where(st => st.Date >= lastWeekStart && st.Date < thisWeekStart);
            });

        var service = new StatsService(studyMock.Object, topicMock.Object, db);
        var result = await service.GetProgressAsync(userId);

        result.Should().NotBeNull();
        var resultType = result.GetType();
        Convert.ToInt32(resultType.GetProperty("thisWeekMinutes")!.GetValue(result)!).Should().Be(180);
        Convert.ToInt32(resultType.GetProperty("lastWeekMinutes")!.GetValue(result)!).Should().Be(80);
        Convert.ToDouble(resultType.GetProperty("changePercent")!.GetValue(result)!).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetExamStreakDaysAsync_CountsConsecutiveDays()
    {
        var userId = 1;
        await using var db = CreateDb();
        var today = DateTime.UtcNow.Date;
        db.ExamResults.Add(new ExamResult
        {
            UserId = userId,
            ExamName = "1",
            Date = DateTime.SpecifyKind(today, DateTimeKind.Utc),
            ExamType = "TYT",
            NetTyt = 1,
            NetAyt = 0,
        });
        db.ExamResults.Add(new ExamResult
        {
            UserId = userId,
            ExamName = "2",
            Date = DateTime.SpecifyKind(today.AddDays(-1), DateTimeKind.Utc),
            ExamType = "TYT",
            NetTyt = 1,
            NetAyt = 0,
        });
        await db.SaveChangesAsync();

        var studyMock = new Mock<IRepository<StudyTime>>();
        var topicMock = new Mock<IRepository<UserTopic>>();
        var service = new StatsService(studyMock.Object, topicMock.Object, db);

        var streak = await service.GetExamStreakDaysAsync(userId);
        streak.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetWinsAsync_GroupsSubjects()
    {
        var userId = 1;
        await using var db = CreateDb();
        db.Topics.Add(new Topic { Id = 1, Name = "A", Category = "TYT", Subject = "Matematik" });
        db.Topics.Add(new Topic { Id = 2, Name = "B", Category = "TYT", Subject = "Matematik" });
        db.UserTopics.AddRange(
            new UserTopic { UserId = userId, TopicId = 1, Status = TopicStatus.Completed },
            new UserTopic { UserId = userId, TopicId = 2, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();

        var studyMock = new Mock<IRepository<StudyTime>>();
        var topicMock = new Mock<IRepository<UserTopic>>();
        var service = new StatsService(studyMock.Object, topicMock.Object, db);

        object wins = await service.GetWinsAsync(userId);
        var sw = wins.GetType().GetProperty("subjectWins")!.GetValue(wins);
        var list = Assert.IsAssignableFrom<System.Collections.IList>(sw);
        list.Count.Should().Be(1);
        var row = list[0]!;
        row.GetType().GetProperty("subject")!.GetValue(row).Should().Be("Matematik");
        row.GetType().GetProperty("completed")!.GetValue(row).Should().Be(1);
        row.GetType().GetProperty("tracked")!.GetValue(row).Should().Be(2);
    }
}
