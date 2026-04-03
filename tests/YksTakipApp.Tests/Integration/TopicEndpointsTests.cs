using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;
using TopicStatus = YksTakipApp.Core.Entities.TopicStatus;

namespace YksTakipApp.Tests.Integration;

public class TopicEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TopicEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Her test için unique email kullan
        var uniqueEmail = $"test{Guid.NewGuid():N}@example.com";
        var user = new User
        {
            Name = "Test User",
            Email = uniqueEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var loginRequest = new LoginRequest
        {
            Email = uniqueEmail,
            Password = "Test123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/users/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginJson = JsonDocument.Parse(loginContent);
        var token = loginJson.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();
        return token!;
    }

    [Fact]
    public async Task GetTopics_ReturnsTopicsList()
    {
        // Arrange - API endpoint'i kullanarak topic ekle (test isolation için)
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var topic1 = new TopicCreateRequest { Name = "Topic 1", Category = "TYT" };
        var topic2 = new TopicCreateRequest { Name = "Topic 2", Category = "AYT" };
        
        await _client.PostAsJsonAsync("/topics", topic1);
        await _client.PostAsJsonAsync("/topics", topic2);

        // Act
        _client.DefaultRequestHeaders.Authorization = null; // Topics endpoint'i public
        var response = await _client.GetAsync("/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        var items = json.RootElement.GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);
        
        var topicNames = items.EnumerateArray()
            .Select(t => t.GetProperty("name").GetString())
            .ToList();
        topicNames.Should().Contain("Topic 1");
        topicNames.Should().Contain("Topic 2");
    }

    [Fact]
    public async Task AddUserTopic_WhenValid_AddsTopicToUser()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var topic = new Topic { Name = "Test Topic", Category = "TYT" };
        db.Topics.Add(topic);
        await db.SaveChangesAsync();

        var request = new UserTopicAddRequest { TopicId = topic.Id };

        // Act
        var response = await _client.PostAsJsonAsync("/user/topics/add", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("eklendi");
    }

    [Fact]
    public async Task AddUserTopic_WhenTopicNotExists_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var request = new UserTopicAddRequest { TopicId = 999 };

        // Act
        var response = await _client.PostAsJsonAsync("/user/topics/add", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateUserTopic_WhenValid_UpdatesStatus()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Önce topic oluştur (API endpoint kullanarak)
        var topicCreateRequest = new TopicCreateRequest { Name = "Test Topic", Category = "TYT" };
        var createResponse = await _client.PostAsJsonAsync("/topics", topicCreateRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Topic ID'yi almak için topics listesini çek
        var topicsResponse = await _client.GetAsync("/topics");
        var topicsContent = await topicsResponse.Content.ReadAsStringAsync();
        var topicsJson = JsonDocument.Parse(topicsContent);
        var topicId = topicsJson.RootElement.GetProperty("items")
            .EnumerateArray()
            .First(t => t.GetProperty("name").GetString() == "Test Topic")
            .GetProperty("id").GetInt32();

        // Önce konuyu kullanıcıya ekle
        var addRequest = new UserTopicAddRequest { TopicId = topicId };
        var addResponse = await _client.PostAsJsonAsync("/user/topics/add", addRequest);
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Şimdi durumu güncelle
        var updateRequest = new UserTopicUpdateRequest
        {
            TopicId = topicId,
            Status = TopicStatus.InProgress
        };

        // Act
        var response = await _client.PostAsJsonAsync("/user/topics/update", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("güncellendi");
    }
}

