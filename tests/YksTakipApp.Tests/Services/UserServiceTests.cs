using FluentAssertions;
using Moq;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<IRepository<User>> _repositoryMock;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _repositoryMock = new Mock<IRepository<User>>();
        _userService = new UserService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetByEmailAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var email = "test@example.com";
        var expectedUser = new User
        {
            Id = 1,
            Name = "Test User",
            Email = email,
            PasswordHash = "hashed_password"
        };

        _repositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(new[] { expectedUser });

        // Act
        var result = await _userService.GetByEmailAsync(email);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be(email);
        result.Name.Should().Be("Test User");
    }

    [Fact]
    public async Task GetByEmailAsync_WhenUserNotExists_ReturnsNull()
    {
        // Arrange
        var email = "nonexistent@example.com";

        _repositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        // Act
        var result = await _userService.GetByEmailAsync(email);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_WhenValidData_CreatesUserWithHashedPassword()
    {
        // Arrange
        var name = "Test User";
        var email = "test@example.com";
        var password = "Test123!";

        User? savedUser = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => savedUser = u)
            .Returns(Task.CompletedTask);
        _repositoryMock
            .Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.RegisterAsync(name, email, password);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Email.Should().Be(email);
        result.PasswordHash.Should().NotBe(password);
        result.PasswordHash.Should().NotBeNullOrEmpty();
        
        // BCrypt hash kontrolü (hash'in password'dan farklı olduğunu doğrula)
        result.PasswordHash.Should().NotContain(password);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public void VerifyPassword_WhenPasswordMatches_ReturnsTrue()
    {
        // Arrange
        var password = "Test123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        // Act
        var result = _userService.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WhenPasswordDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var correctPassword = "Test123!";
        var wrongPassword = "WrongPassword123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(correctPassword);

        // Act
        var result = _userService.VerifyPassword(wrongPassword, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var userId = 1;
        var expectedUser = new User
        {
            Id = userId,
            Name = "Test User",
            Email = "test@example.com",
            PasswordHash = "hashed_password"
        };

        _repositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(new[] { expectedUser });

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserNotExists_ReturnsNull()
    {
        // Arrange
        var userId = 999;

        _repositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<User>());

        // Act
        var result = await _userService.GetByIdAsync(userId);

        // Assert
        result.Should().BeNull();
    }
}

