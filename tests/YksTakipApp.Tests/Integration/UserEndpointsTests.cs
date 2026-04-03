using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

public class UserEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WhenValidData_ReturnsSuccess()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "Test User",
            Email = "test@example.com",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Kayıt başarılı");
    }

    [Fact]
    public async Task Register_WhenEmailExists_ReturnsBadRequest()
    {
        // Arrange - İlk register (başarılı olmalı)
        var firstRequest = new RegisterRequest
        {
            Name = "First User",
            Email = $"first{Guid.NewGuid():N}@example.com",
            Password = "Test123!"
        };
        var firstResponse = await _client.PostAsJsonAsync("/users/register", firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // İkinci register (aynı email - başarısız olmalı)
        var secondRequest = new RegisterRequest
        {
            Name = "Second User",
            Email = firstRequest.Email, // Aynı email
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users/register", secondRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("zaten kayıtlı");
    }

    [Fact]
    public async Task Register_WhenInvalidData_ReturnsValidationError()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Name = "", // Geçersiz
            Email = "invalid-email", // Geçersiz
            Password = "123" // Çok kısa
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WhenValidCredentials_ReturnsToken()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Kullanıcı oluştur
        var user = new User
        {
            Name = "Test User",
            Email = "testlogin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "testlogin@example.com",
            Password = "Test123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        json.RootElement.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("message").GetString().Should().Contain("Giriş başarılı");
    }

    [Fact]
    public async Task Login_WhenInvalidCredentials_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/users/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetMe_WhenAuthenticated_ReturnsUserProfile()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Kullanıcı oluştur
        var user = new User
        {
            Name = "Test User",
            Email = "testme@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Login yap ve token al
        var loginRequest = new LoginRequest
        {
            Email = "testme@example.com",
            Password = "Test123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/users/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginContent = await loginResponse.Content.ReadAsStringAsync();
        var loginJson = JsonDocument.Parse(loginContent);
        var token = loginJson.RootElement.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();

        // Act
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("testme@example.com");
        content.Should().Contain("Test User");
    }

    [Fact]
    public async Task GetMe_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

