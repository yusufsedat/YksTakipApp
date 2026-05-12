using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

public class AnalyticsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AnalyticsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var uniqueEmail = $"analytics{Guid.NewGuid():N}@example.com";
        db.Users.Add(new User
        {
            Name = "Analytics User",
            Email = uniqueEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!")
        });
        await db.SaveChangesAsync();

        var loginResponse = await _client.PostAsJsonAsync("/users/login", new LoginRequest
        {
            Email = uniqueEmail,
            Password = "Test123!"
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await loginResponse.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task ChurnSummary_WithoutAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/analytics/churn/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChurnSummary_WithInvalidDateRange_ReturnsBadRequest()
    {
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/analytics/churn/summary?from=2026-05-10&to=2026-05-01");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
