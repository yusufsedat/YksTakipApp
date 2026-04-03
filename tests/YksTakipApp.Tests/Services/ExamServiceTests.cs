using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public class ExamServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddExamAsync_WhenValidData_CreatesExam()
    {
        var userId = 1;
        var examName = "TYT Deneme 1";
        var date = DateTime.UtcNow;
        var netTyt = 85.5;
        var netAyt = 78.0;

        await using var db = CreateDb();
        var examService = new ExamService(db);

        await examService.AddExamAsync(
            userId,
            examName,
            date,
            netTyt,
            netAyt,
            "TYT",
            subject: null,
            durationMinutes: null,
            difficulty: null,
            errorReasons: null,
            details: null);

        var savedExam = await db.ExamResults.SingleAsync();
        savedExam.UserId.Should().Be(userId);
        savedExam.ExamName.Should().Be(examName);
        savedExam.NetTyt.Should().Be(netTyt);
        savedExam.NetAyt.Should().Be(netAyt);
        savedExam.ExamType.Should().Be("TYT");
        savedExam.Date.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetUserExamsAsync_WhenUserHasExams_ReturnsExams()
    {
        var userId = 1;
        await using var db = CreateDb();
        db.ExamResults.AddRange(
            new ExamResult
            {
                UserId = userId,
                ExamName = "TYT Deneme 1",
                Date = DateTime.UtcNow,
                ExamType = "TYT",
                NetTyt = 80,
                NetAyt = 75,
            },
            new ExamResult
            {
                UserId = userId,
                ExamName = "TYT Deneme 2",
                Date = DateTime.UtcNow.AddDays(-1),
                ExamType = "TYT",
                NetTyt = 85,
                NetAyt = 80,
            });
        await db.SaveChangesAsync();

        var examService = new ExamService(db);
        var result = (await examService.GetUserExamsAsync(userId)).ToList();

        result.Should().HaveCount(2);
        result.All(e => e.UserId == userId).Should().BeTrue();
    }

    [Fact]
    public async Task GetUserExamsAsync_WhenUserHasNoExams_ReturnsEmpty()
    {
        await using var db = CreateDb();
        var examService = new ExamService(db);
        var result = await examService.GetUserExamsAsync(1);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteExamAsync_WhenExamExists_DeletesExam()
    {
        var userId = 1;
        await using var db = CreateDb();
        db.ExamResults.Add(new ExamResult
        {
            UserId = userId,
            ExamName = "TYT Deneme 1",
            Date = DateTime.UtcNow,
            ExamType = "TYT",
            NetTyt = 80,
            NetAyt = 75,
        });
        await db.SaveChangesAsync();
        var examId = (await db.ExamResults.SingleAsync()).Id;

        var examService = new ExamService(db);
        await examService.DeleteExamAsync(userId, examId);

        (await db.ExamResults.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteExamAsync_WhenExamNotExists_DoesNothing()
    {
        await using var db = CreateDb();
        var examService = new ExamService(db);
        await examService.DeleteExamAsync(1, 999);
        (await db.ExamResults.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteExamAsync_WhenExamBelongsToDifferentUser_DoesNothing()
    {
        await using var db = CreateDb();
        db.ExamResults.Add(new ExamResult
        {
            UserId = 999,
            ExamName = "TYT Deneme 1",
            Date = DateTime.UtcNow,
            ExamType = "TYT",
            NetTyt = 80,
            NetAyt = 75,
        });
        await db.SaveChangesAsync();
        var examId = (await db.ExamResults.SingleAsync()).Id;

        var examService = new ExamService(db);
        await examService.DeleteExamAsync(userId: 1, examId);

        (await db.ExamResults.CountAsync()).Should().Be(1);
    }
}
