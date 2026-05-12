using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

/// <summary>
/// API contract tests for planner / topic-priority / study-time / exam endpoints.
/// Notlar:
///  - InMemory provider <c>ExecuteUpdateAsync</c> desteklemediği için PATCH testi <see cref="ScheduleTaskStatus.Skipped"/> üzerinden ilerletilir.
///  - Her test unique email + unique topic ile çalışır; class fixture aynı InMemory DB'yi paylaşır.
/// </summary>
public sealed class PlannerEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public PlannerEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(int userId, string token)> CreateAuthenticatedUserAsync()
    {
        // /users/login rate-limit'li (5/dk); JWT'yi doğrudan üretip rate-limit kuyruğunu by-pass ediyoruz.
        var email = $"planner{Guid.NewGuid():N}@example.com";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var user = new User
        {
            Name = "Planner Test",
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = "User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var token = JwtHelper.GenerateToken(user, config, TimeSpan.FromHours(1));
        return (user.Id, token);
    }

    private async Task<int> SeedUserTopicAsync(int userId, string subject = "Matematik")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var topic = new Topic { Name = $"Konu-{Guid.NewGuid():N}", Category = "TYT", Subject = subject };
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        db.UserTopics.Add(new UserTopic { UserId = userId, TopicId = topic.Id, Status = TopicStatus.InProgress });
        await db.SaveChangesAsync();
        return topic.Id;
    }

    private async Task SeedActiveGoalAsync(int userId, int dailyMinutes = 180)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goalId = Guid.NewGuid();
        db.UserGoals.Add(new Core.Entities.UserGoal
        {
            Id = goalId,
            UserId = userId,
            TargetUniversity = "x",
            TargetDepartment = "x",
            DailyAvailableMinutes = dailyMinutes,
            CreatedAt = DateTime.UtcNow
        });
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.ActiveGoalVersionId = goalId;
        await db.SaveChangesAsync();
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    // ---------- POST /planner/generate ----------

    [Fact]
    public async Task PostPlannerGenerate_WhenAuthenticatedWithGoal_Returns200WithSuccessEnvelope()
    {
        var (userId, token) = await CreateAuthenticatedUserAsync();
        await SeedActiveGoalAsync(userId);
        await SeedUserTopicAsync(userId);
        SetAuth(token);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await _client.PostAsJsonAsync("/planner/generate", new GenerateWeeklyPlanRequest
        {
            StartDate = today
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("tasks").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task PostPlannerGenerate_WhenNoGoal_Returns422WithRequiresGoalReason()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/planner/generate", new GenerateWeeklyPlanRequest
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be("noPlanGenerated");
        doc.RootElement.GetProperty("reasonCode").GetString().Should().Be("requiresGoal");
        doc.RootElement.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PostPlannerGenerate_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/planner/generate", new GenerateWeeklyPlanRequest
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPlannerGenerate_WithStartDateTooFarInPast_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/planner/generate", new GenerateWeeklyPlanRequest
        {
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- GET /planner/weekly ----------

    [Fact]
    public async Task GetPlannerWeekly_WhenAuthenticated_Returns200WithArray()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = today.AddDays(6);
        var response = await _client.GetAsync($"/planner/weekly?start={today:yyyy-MM-dd}&end={end:yyyy-MM-dd}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetPlannerWeekly_WithoutQueryParams_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.GetAsync("/planner/weekly");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPlannerWeekly_WithRangeExceeding14Days_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = today.AddDays(20);
        var response = await _client.GetAsync($"/planner/weekly?start={today:yyyy-MM-dd}&end={end:yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetPlannerWeekly_WithEndBeforeStart_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await _client.GetAsync($"/planner/weekly?start={today:yyyy-MM-dd}&end={today.AddDays(-1):yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- PATCH /planner/tasks/{taskId}/status ----------

    [Fact]
    public async Task PatchPlannerTaskStatus_WhenValid_Returns200WithUpdatedDto()
    {
        var (userId, token) = await CreateAuthenticatedUserAsync();
        var topicId = await SeedUserTopicAsync(userId);
        SetAuth(token);

        int taskId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var task = new ScheduleTask
            {
                UserId = userId,
                TopicId = topicId,
                TaskDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DurationMinutes = 30,
                Status = ScheduleTaskStatus.Planned,
                TaskType = TaskType.Study
            };
            db.ScheduleTasks.Add(task);
            await db.SaveChangesAsync();
            taskId = task.Id;
        }

        // Skipped seçildi: Completed kolu InMemory'de desteklenmeyen ExecuteUpdateAsync çağırıyor.
        var response = await _client.PatchAsJsonAsync($"/planner/tasks/{taskId}/status",
            new UpdateScheduleTaskStatusRequest { Status = ScheduleTaskStatus.Skipped });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ScheduleTaskDto>(JsonOpts);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(taskId);
        dto.Status.Should().Be(ScheduleTaskStatus.Skipped);
    }

    [Fact]
    public async Task PatchPlannerTaskStatus_WhenTaskNotFound_Returns404()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PatchAsJsonAsync("/planner/tasks/999999/status",
            new UpdateScheduleTaskStatusRequest { Status = ScheduleTaskStatus.Skipped });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PatchPlannerTaskStatus_WithInvalidEnum_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        // Enum dışı değer; FluentValidation IsInEnum() kuralı 400 döner.
        var response = await _client.PatchAsync("/planner/tasks/1/status",
            JsonContent.Create(new { status = 999 }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- POST /topics/{topicId}/request-priority ----------

    [Fact]
    public async Task PostRequestPriority_WhenTopicInUserList_Returns200AndRegeneratesPlan()
    {
        var (userId, token) = await CreateAuthenticatedUserAsync();
        var topicId = await SeedUserTopicAsync(userId);
        SetAuth(token);

        var response = await _client.PostAsync($"/topics/{topicId}/request-priority", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("message").GetString().Should().Contain("öncelik");
        doc.RootElement.GetProperty("tasks").ValueKind.Should().Be(JsonValueKind.Array);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ut = await db.UserTopics.AsNoTracking()
            .FirstAsync(x => x.UserId == userId && x.TopicId == topicId);
        ut.IsPriorityRequested.Should().BeTrue();
        ut.PriorityExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PostRequestPriority_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync("/topics/1/request-priority", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostRequestPriority_WhenTopicNotInUserList_Returns404()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsync("/topics/987654/request-priority", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---------- POST /studytime/create ----------

    [Fact]
    public async Task PostStudyTimeCreate_WhenValid_Returns200WithItem()
    {
        var (userId, token) = await CreateAuthenticatedUserAsync();
        var topicId = await SeedUserTopicAsync(userId);
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/studytime/create", new StudyTimeRequest
        {
            Date = DateTime.UtcNow,
            DurationMinutes = 45,
            TopicId = topicId,
            ClientRequestId = $"st-{Guid.NewGuid():N}"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("replay").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("item").GetProperty("durationMinutes").GetInt32().Should().Be(45);
    }

    [Fact]
    public async Task PostStudyTimeCreate_WithFutureDate_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/studytime/create", new StudyTimeRequest
        {
            Date = DateTime.UtcNow.AddDays(5),
            DurationMinutes = 30
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostStudyTimeCreate_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/studytime/create", new StudyTimeRequest
        {
            Date = DateTime.UtcNow,
            DurationMinutes = 30
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- POST /exam/add ----------

    [Fact]
    public async Task PostExamAdd_WhenValid_Returns200WithItemId()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/exam/add", new ExamResultRequest
        {
            ExamName = "TYT Deneme 1",
            ExamType = "TYT",
            Date = DateTime.UtcNow,
            NetTyt = 95.5,
            NetAyt = 0,
            DurationMinutes = 135,
            Difficulty = 3,
            ClientRequestId = $"ex-{Guid.NewGuid():N}",
            Details = new List<ExamDetailInput>
            {
                new() { Subject = "Matematik", Correct = 30, Wrong = 5, Blank = 5 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("message").GetString().Should().Contain("Deneme");
        doc.RootElement.GetProperty("itemId").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PostExamAdd_WithInvalidExamType_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/exam/add", new ExamResultRequest
        {
            ExamName = "Hatalı Deneme",
            ExamType = "FOO",
            Date = DateTime.UtcNow,
            NetTyt = 0,
            NetAyt = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostExamAdd_WhenBransWithoutSubject_Returns400()
    {
        var (_, token) = await CreateAuthenticatedUserAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/exam/add", new ExamResultRequest
        {
            ExamName = "Branş Deneme",
            ExamType = "BRANS",
            Subject = null,
            Date = DateTime.UtcNow,
            NetTyt = 0,
            NetAyt = 10
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostExamAdd_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/exam/add", new ExamResultRequest
        {
            ExamName = "X",
            ExamType = "TYT",
            Date = DateTime.UtcNow
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
