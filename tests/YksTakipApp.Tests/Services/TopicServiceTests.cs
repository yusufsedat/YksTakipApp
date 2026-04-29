using FluentAssertions;
using Moq;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Tests.Services;

public class TopicServiceTests
{
    private readonly Mock<IRepository<Topic>> _topicRepositoryMock;
    private readonly Mock<IRepository<UserTopic>> _userTopicRepositoryMock;
    private readonly TopicService _topicService;

    public TopicServiceTests()
    {
        _topicRepositoryMock = new Mock<IRepository<Topic>>();
        _userTopicRepositoryMock = new Mock<IRepository<UserTopic>>();
        _topicService = new TopicService(_topicRepositoryMock.Object, _userTopicRepositoryMock.Object);
    }

    [Fact]
    public async Task AddTopicAsync_WhenValidData_CreatesTopic()
    {
        // Arrange
        var name = "Matematik - Fonksiyonlar";
        var category = "TYT";

        Topic? savedTopic = null;
        _topicRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Topic>()))
            .Callback<Topic>(t => savedTopic = t)
            .Returns(Task.CompletedTask);
        _topicRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _topicService.AddTopicAsync(name, category);

        // Assert
        savedTopic.Should().NotBeNull();
        savedTopic!.Name.Should().Be(name);
        savedTopic.Category.Should().Be(category);

        _topicRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Topic>()), Times.Once);
        _topicRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTopics()
    {
        // Arrange
        var expectedTopics = new List<Topic>
        {
            new Topic { Id = 1, Name = "Topic 1", Category = "TYT" },
            new Topic { Id = 2, Name = "Topic 2", Category = "AYT" }
        };

        _topicRepositoryMock
            .Setup(r => r.GetAllForReadAsync())
            .ReturnsAsync(expectedTopics);

        // Act
        var result = await _topicService.GetAllAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(expectedTopics);
    }

    [Fact]
    public async Task GetUserTopicsAsync_WhenUserHasTopics_ReturnsUserTopics()
    {
        // Arrange
        var userId = 1;
        var expectedUserTopics = new List<UserTopic>
        {
            new UserTopic { UserId = userId, TopicId = 1, Status = TopicStatus.NotStarted },
            new UserTopic { UserId = userId, TopicId = 2, Status = TopicStatus.InProgress }
        };

        _userTopicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(expectedUserTopics);

        // Act
        var result = await _topicService.GetUserTopicsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(expectedUserTopics);
    }

    [Fact]
    public async Task AddUserTopicAsync_WhenTopicExistsAndNotAdded_AddsUserTopic()
    {
        // Arrange
        var userId = 1;
        var topicId = 1;
        var topic = new Topic { Id = topicId, Name = "Test Topic", Category = "TYT" };

        _topicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Topic, bool>>>()))
            .ReturnsAsync(new[] { topic });

        _userTopicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<UserTopic>());

        UserTopic? savedUserTopic = null;
        _userTopicRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<UserTopic>()))
            .Callback<UserTopic>(ut => savedUserTopic = ut)
            .Returns(Task.CompletedTask);
        _userTopicRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _topicService.AddUserTopicAsync(userId, topicId);

        // Assert
        savedUserTopic.Should().NotBeNull();
        savedUserTopic!.UserId.Should().Be(userId);
        savedUserTopic.TopicId.Should().Be(topicId);
        savedUserTopic.Status.Should().Be(TopicStatus.NotStarted);

        _userTopicRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserTopic>()), Times.Once);
        _userTopicRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddUserTopicAsync_WhenTopicNotExists_ThrowsException()
    {
        // Arrange
        var userId = 1;
        var topicId = 999;

        _topicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Topic, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<Topic>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _topicService.AddUserTopicAsync(userId, topicId));

        _userTopicRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserTopic>()), Times.Never);
    }

    [Fact]
    public async Task AddUserTopicAsync_WhenTopicAlreadyAdded_ThrowsException()
    {
        // Arrange
        var userId = 1;
        var topicId = 1;
        var topic = new Topic { Id = topicId, Name = "Test Topic", Category = "TYT" };
        var existingUserTopic = new UserTopic { UserId = userId, TopicId = topicId, Status = TopicStatus.NotStarted };

        _topicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Topic, bool>>>()))
            .ReturnsAsync(new[] { topic });

        _userTopicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(new[] { existingUserTopic });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _topicService.AddUserTopicAsync(userId, topicId));

        _userTopicRepositoryMock.Verify(r => r.AddAsync(It.IsAny<UserTopic>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserTopicAsync_WhenUserTopicExists_UpdatesStatus()
    {
        // Arrange
        var userId = 1;
        var topicId = 1;
        var newStatus = TopicStatus.Completed;
        var existingUserTopic = new UserTopic 
        { 
            UserId = userId, 
            TopicId = topicId, 
            Status = TopicStatus.InProgress 
        };

        _userTopicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(new[] { existingUserTopic });
        _userTopicRepositoryMock
            .Setup(r => r.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _topicService.UpdateUserTopicAsync(userId, topicId, newStatus);

        // Assert
        existingUserTopic.Status.Should().Be(newStatus);
        _userTopicRepositoryMock.Verify(r => r.Update(It.IsAny<UserTopic>()), Times.Once);
        _userTopicRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateUserTopicAsync_WhenUserTopicNotExists_ThrowsException()
    {
        // Arrange
        var userId = 1;
        var topicId = 999;
        var newStatus = TopicStatus.Completed;

        _userTopicRepositoryMock
            .Setup(r => r.FindForReadAsync(It.IsAny<System.Linq.Expressions.Expression<Func<UserTopic, bool>>>()))
            .ReturnsAsync(Enumerable.Empty<UserTopic>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _topicService.UpdateUserTopicAsync(userId, topicId, newStatus));

        _userTopicRepositoryMock.Verify(r => r.Update(It.IsAny<UserTopic>()), Times.Never);
        _userTopicRepositoryMock.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}

