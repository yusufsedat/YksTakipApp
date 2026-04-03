using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public class StudyTimeServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedUserAsync(AppDbContext db, int id = 1)
    {
        db.Users.Add(new User
        {
            Id = id,
            Name = "Test",
            Email = $"u{id}@t.com",
            PasswordHash = "x",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AddStudyTimeAsync_WhenValidData_CreatesStudyTime()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var svc = new StudyTimeService(db);
        var date = DateTime.UtcNow.Date.AddHours(12);

        await svc.AddStudyTimeAsync(1, 120, date, null);

        var st = await db.StudyTimes.SingleAsync();
        st.UserId.Should().Be(1);
        st.DurationMinutes.Should().Be(120);
        st.Date.Kind.Should().Be(DateTimeKind.Utc);
        st.TopicId.Should().BeNull();
    }

    [Fact]
    public async Task AddStudyTimeAsync_WhenTopicInUserList_SetsTopicId()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        db.Topics.Add(new Topic { Id = 5, Name = "Paragraf", Category = "TYT", Subject = "Türkçe" });
        db.UserTopics.Add(new UserTopic { UserId = 1, TopicId = 5, Status = 0 });
        await db.SaveChangesAsync();
        var svc = new StudyTimeService(db);

        await svc.AddStudyTimeAsync(1, 60, DateTime.UtcNow.Date.AddHours(12), 5);

        (await db.StudyTimes.SingleAsync()).TopicId.Should().Be(5);
    }

    [Fact]
    public async Task AddStudyTimeAsync_WhenTopicNotInUserList_Throws()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        db.Topics.Add(new Topic { Id = 5, Name = "Paragraf", Category = "TYT", Subject = "Türkçe" });
        await db.SaveChangesAsync();
        var svc = new StudyTimeService(db);

        var act = async () => await svc.AddStudyTimeAsync(1, 60, DateTime.UtcNow.Date.AddHours(12), 5);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetStudyTimesAsync_WhenUserHasStudyTimes_ReturnsStudyTimes()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        db.StudyTimes.AddRange(
            new StudyTime { UserId = 1, DurationMinutes = 60, Date = DateTime.UtcNow.AddDays(-1) },
            new StudyTime { UserId = 1, DurationMinutes = 90, Date = DateTime.UtcNow.AddDays(-2) }
        );
        await db.SaveChangesAsync();
        var svc = new StudyTimeService(db);

        var result = await svc.GetStudyTimesAsync(1);

        result.Should().HaveCount(2);
        result.All(st => st.UserId == 1).Should().BeTrue();
    }

    [Fact]
    public async Task GetStudyTimesAsync_WhenUserHasNoStudyTimes_ReturnsEmpty()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var svc = new StudyTimeService(db);

        var result = await svc.GetStudyTimesAsync(1);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTotalMinutesLast7DaysAsync_WhenUserHasStudyTimes_ReturnsTotal()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        db.StudyTimes.AddRange(
            new StudyTime { UserId = 1, DurationMinutes = 60, Date = DateTime.UtcNow.AddDays(-1) },
            new StudyTime { UserId = 1, DurationMinutes = 90, Date = DateTime.UtcNow.AddDays(-2) },
            new StudyTime { UserId = 1, DurationMinutes = 30, Date = DateTime.UtcNow.AddDays(-3) },
            new StudyTime { UserId = 1, DurationMinutes = 120, Date = DateTime.UtcNow.AddDays(-8) }
        );
        await db.SaveChangesAsync();
        var svc = new StudyTimeService(db);

        var result = await svc.GetTotalMinutesLast7DaysAsync(1);

        result.Should().Be(180);
    }

    [Fact]
    public async Task GetTotalMinutesLast7DaysAsync_WhenUserHasNoStudyTimes_ReturnsZero()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        var svc = new StudyTimeService(db);

        var result = await svc.GetTotalMinutesLast7DaysAsync(1);

        result.Should().Be(0);
    }

    [Fact]
    public async Task GetTotalMinutesLast7DaysAsync_WhenOnlyOldStudyTimes_ReturnsZero()
    {
        await using var db = CreateDb();
        await SeedUserAsync(db);
        db.StudyTimes.AddRange(
            new StudyTime { UserId = 1, DurationMinutes = 60, Date = DateTime.UtcNow.AddDays(-8) },
            new StudyTime { UserId = 1, DurationMinutes = 90, Date = DateTime.UtcNow.AddDays(-10) }
        );
        await db.SaveChangesAsync();
        var svc = new StudyTimeService(db);

        var result = await svc.GetTotalMinutesLast7DaysAsync(1);

        result.Should().Be(0);
    }
}
