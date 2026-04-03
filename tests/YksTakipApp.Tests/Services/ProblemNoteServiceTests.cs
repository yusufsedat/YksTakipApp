using FluentAssertions;
using Moq;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Tests.Services;

public class ProblemNoteServiceTests
{
    private readonly Mock<IRepository<ProblemNote>> _repo = new();
    private readonly ProblemNoteService _service;

    public ProblemNoteServiceTests()
    {
        _service = new ProblemNoteService(_repo.Object);
    }

    [Fact]
    public async Task AddAsync_StoresImageAndTagsJson()
    {
        ProblemNote? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<ProblemNote>()))
            .Callback<ProblemNote>(n => saved = n)
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var img = "data:image/jpeg;base64," + new string('A', 120);
        await _service.AddAsync(3, img, new[] { "Matematik", "Türev" }, false);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(3);
        saved.ImageBase64.Should().StartWith("data:image/jpeg;base64,");
        saved.TagsJson.ToLowerInvariant().Should().Contain("matematik");
        saved.SolutionLearned.Should().BeFalse();
    }
}
