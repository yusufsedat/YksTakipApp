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
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

public sealed class AdminDecisionLogStatsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AdminDecisionLogStatsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var user = new User
        {
            Name = "Admin Stats",
            Email = $"admin-stats-{Guid.NewGuid():N}@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = "Admin"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return JwtHelper.GenerateToken(user, config, TimeSpan.FromHours(1));
    }

    private async Task<string> CreateUserTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var user = new User
        {
            Name = "Stats User",
            Email = $"user-stats-{Guid.NewGuid():N}@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            Role = "User"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return JwtHelper.GenerateToken(user, config, TimeSpan.FromHours(1));
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    private async Task SeedLogAsync(PlanGenerationStatus status, PlanGenerationReasonCode reason,
        int? quality = null, PlanQualityBand? band = null,
        int priorityActive = 0, int priorityPlaced = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.PlannerDecisionLogs.Add(new PlannerDecisionLog
        {
            UserId = 1,
            WeekStart = today,
            WeekEnd = today.AddDays(6),
            Status = status,
            ReasonCode = reason,
            QualityScore = quality,
            QualityBand = band,
            PriorityActiveCount = priorityActive,
            PriorityPlacedCount = priorityPlaced,
            DurationMs = 10,
            BreakdownJson = "{}"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task NonAdmin_Gets403()
    {
        var token = await CreateUserTokenAsync();
        SetAuth(token);
        var resp = await _client.GetAsync("/admin/planner/decision-logs/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_GetsAggregateStats_WithReasonAndQualityAndPriority()
    {
        await SeedLogAsync(PlanGenerationStatus.NoPlanGenerated, PlanGenerationReasonCode.NoTopics);
        await SeedLogAsync(PlanGenerationStatus.NoPlanGenerated, PlanGenerationReasonCode.NoTopics);
        await SeedLogAsync(PlanGenerationStatus.NoPlanGenerated, PlanGenerationReasonCode.RequiresGoal);
        await SeedLogAsync(PlanGenerationStatus.Success, PlanGenerationReasonCode.None,
            quality: 80, band: PlanQualityBand.Healthy, priorityActive: 2, priorityPlaced: 2);
        await SeedLogAsync(PlanGenerationStatus.Success, PlanGenerationReasonCode.None,
            quality: 50, band: PlanQualityBand.Warning, priorityActive: 3, priorityPlaced: 1);
        await SeedLogAsync(PlanGenerationStatus.Success, PlanGenerationReasonCode.None,
            quality: 30, band: PlanQualityBand.Risky, priorityActive: 0, priorityPlaced: 0);

        var token = await CreateAdminTokenAsync();
        SetAuth(token);
        var resp = await _client.GetAsync("/admin/planner/decision-logs/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        stats.GetProperty("totalCalls").GetInt32().Should().BeGreaterOrEqualTo(6);
        stats.GetProperty("successCount").GetInt32().Should().BeGreaterOrEqualTo(3);
        stats.GetProperty("noPlanCount").GetInt32().Should().BeGreaterOrEqualTo(3);

        var reasons = stats.GetProperty("topNoPlanReasons").EnumerateArray()
            .Select(e => (Code: e.GetProperty("reasonCode").GetString(), Count: e.GetProperty("count").GetInt32()))
            .ToList();
        reasons.Should().Contain(r => r.Code == "noTopics" && r.Count >= 2);
        reasons[0].Count.Should().BeGreaterOrEqualTo(reasons[^1].Count);

        stats.GetProperty("avgQualityScore").GetDouble().Should().BeGreaterThan(0);
        var dist = stats.GetProperty("qualityBandDistribution");
        dist.GetProperty("healthy").GetInt32().Should().BeGreaterOrEqualTo(1);
        dist.GetProperty("warning").GetInt32().Should().BeGreaterOrEqualTo(1);
        dist.GetProperty("risky").GetInt32().Should().BeGreaterOrEqualTo(1);

        stats.GetProperty("priorityFulfillmentRate").GetDouble().Should().BeInRange(0, 1);
        stats.GetProperty("callsWithUnplacedPriority").GetInt32().Should().BeGreaterOrEqualTo(1);
    }
}
