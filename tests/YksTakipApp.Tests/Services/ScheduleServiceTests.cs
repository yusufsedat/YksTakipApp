using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;
using YksTakipApp.Infra.Repositories;

namespace YksTakipApp.Tests.Services;

public class ScheduleServiceTests
{
    [Fact]
    public async Task AddAsync_PersistsWeeklyEntry()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ScheduleTests_" + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options);
        var repo = new Repository<ScheduleEntry>(db);
        var service = new ScheduleService(repo, db);

        var result = await service.AddAsync(5, "Weekly", 1, null, 540, 600, "Matematik", null);

        result.UserId.Should().Be(5);
        result.Recurrence.Should().Be("Weekly");
        result.DayOfWeek.Should().Be(1);
        result.DayOfMonth.Should().BeNull();
        result.Title.Should().Be("Matematik");
        result.TopicId.Should().BeNull();
        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddAsync_WithTopicId_RequiresUserTopic()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ScheduleTests_" + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options);
        db.Topics.Add(new Topic { Id = 1, Name = "Paragraf", Category = "TYT", Subject = "Türkçe" });
        db.Users.Add(new User { Id = 10, Name = "U", Email = "u@test.com", PasswordHash = "x", Role = "User" });
        await db.SaveChangesAsync();

        var repo = new Repository<ScheduleEntry>(db);
        var service = new ScheduleService(repo, db);

        var act = async () => await service.AddAsync(10, "Weekly", 1, null, 540, 600, "Paragraf", 1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Konular*");
    }

    [Fact]
    public async Task AddAsync_WithTopicId_WhenInUserList_Persists()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ScheduleTests_" + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options);
        db.Topics.Add(new Topic { Id = 1, Name = "Paragraf", Category = "TYT", Subject = "Türkçe" });
        db.Users.Add(new User { Id = 10, Name = "U", Email = "u@test.com", PasswordHash = "x", Role = "User" });
        db.UserTopics.Add(new UserTopic { UserId = 10, TopicId = 1, Status = TopicStatus.NotStarted });
        await db.SaveChangesAsync();

        var repo = new Repository<ScheduleEntry>(db);
        var service = new ScheduleService(repo, db);

        var result = await service.AddAsync(10, "Weekly", 1, null, 540, 600, "Paragraf", 1);

        result.TopicId.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_Throws()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ScheduleTests_" + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options);
        var repo = new Repository<ScheduleEntry>(db);
        var service = new ScheduleService(repo, db);

        var act = async () => await service.DeleteAsync(1, 99);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bulunamadı*");
    }

    [Fact]
    public async Task GetListAsync_IncludesTopicNavigation()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ScheduleTests_" + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options);
        db.Topics.Add(new Topic { Id = 2, Name = "Türev", Category = "AYT", Subject = "Matematik" });
        db.Users.Add(new User { Id = 20, Name = "U2", Email = "u2@test.com", PasswordHash = "x", Role = "User" });
        db.UserTopics.Add(new UserTopic { UserId = 20, TopicId = 2, Status = TopicStatus.NotStarted });
        db.ScheduleEntries.Add(new ScheduleEntry
        {
            UserId = 20,
            Recurrence = "Weekly",
            DayOfWeek = 2,
            StartMinute = 600,
            EndMinute = 660,
            Title = "Türev",
            TopicId = 2,
        });
        await db.SaveChangesAsync();

        var repo = new Repository<ScheduleEntry>(db);
        var service = new ScheduleService(repo, db);

        var list = await service.GetListAsync(20);

        list.Should().HaveCount(1);
        list[0].Topic.Should().NotBeNull();
        list[0].Topic!.Name.Should().Be("Türev");
    }
}
