using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

public sealed class AdminPlannerDebugTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AdminPlannerDebugTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(int userId, string token)> CreateUserAsync(string role)
    {
        var email = $"debug{role}-{Guid.NewGuid():N}@example.com";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var user = new User
        {
            Name = $"Debug {role}",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = role,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var token = JwtHelper.GenerateToken(user, config, TimeSpan.FromHours(1));
        return (user.Id, token);
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task NonAdmin_Gets403()
    {
        var (_, userToken) = await CreateUserAsync("User");
        var (targetId, _) = await CreateUserAsync("User");
        SetAuth(userToken);

        var resp = await _client.GetAsync($"/admin/users/{targetId}/planner-debug");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_EmptyUser_ReturnsEmptyLists()
    {
        var (_, adminToken) = await CreateUserAsync("Admin");
        var (targetId, _) = await CreateUserAsync("User");
        SetAuth(adminToken);

        var resp = await _client.GetAsync($"/admin/users/{targetId}/planner-debug");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("user").GetProperty("id").GetInt32().Should().Be(targetId);
        root.GetProperty("activeGoal").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("latestPlan").GetArrayLength().Should().Be(0);
        root.GetProperty("priorityRequests").GetArrayLength().Should().Be(0);
        root.GetProperty("recentChurnEvents").GetArrayLength().Should().Be(0);
        // featureFlags object'i mevcut (boş olabilir).
        root.TryGetProperty("featureFlags", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Admin_UnknownUser_Returns404()
    {
        var (_, adminToken) = await CreateUserAsync("Admin");
        SetAuth(adminToken);

        var resp = await _client.GetAsync("/admin/users/999999/planner-debug");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_PopulatedUser_ReturnsAllSections()
    {
        var (_, adminToken) = await CreateUserAsync("Admin");
        var (targetId, _) = await CreateUserAsync("User");
        SetAuth(adminToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var goalId = Guid.NewGuid();
            db.UserGoals.Add(new UserGoal
            {
                Id = goalId,
                UserId = targetId,
                TargetUniversity = "x",
                TargetDepartment = "x",
                DailyAvailableMinutes = 240,
                CreatedAt = DateTime.UtcNow
            });
            var user = await db.Users.FirstAsync(u => u.Id == targetId);
            user.ActiveGoalVersionId = goalId;

            var topic = new Topic { Name = $"T-{Guid.NewGuid():N}", Subject = "Mat", Category = "TYT" };
            db.Topics.Add(topic);
            await db.SaveChangesAsync();

            db.UserTopics.Add(new UserTopic
            {
                UserId = targetId,
                TopicId = topic.Id,
                IsPriorityRequested = true,
                PriorityRequestedAt = DateTime.UtcNow,
                PriorityExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            db.ScheduleTasks.Add(new ScheduleTask
            {
                UserId = targetId,
                TopicId = topic.Id,
                TaskDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DurationMinutes = 30,
                Status = ScheduleTaskStatus.Planned,
                TaskType = TaskType.Study
            });
            await db.SaveChangesAsync();
        }

        var resp = await _client.GetAsync($"/admin/users/{targetId}/planner-debug");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("activeGoal").GetProperty("dailyAvailableMinutes").GetInt32().Should().Be(240);
        root.GetProperty("priorityRequests").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("latestPlan").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("capacity").GetProperty("workingDaily").GetInt32().Should().BeGreaterThan(0);
    }
}
