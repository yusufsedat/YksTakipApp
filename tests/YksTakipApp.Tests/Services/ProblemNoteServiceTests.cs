using FluentAssertions;
using Moq;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Tests.Services;

public class ProblemNoteServiceTests
{
    private readonly Mock<IRepository<ProblemNote>> _repo = new();
    private readonly Mock<IProblemNoteImageStorage> _images = new();
    private readonly ProblemNoteService _service;

    public ProblemNoteServiceTests()
    {
        _service = new ProblemNoteService(_repo.Object, _images.Object);
    }

    [Fact]
    public async Task AddAsync_StoresImageUrlAndTagsJson()
    {
        ProblemNote? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<ProblemNote>()))
            .Callback<ProblemNote>(n => saved = n)
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var img = "data:image/jpeg;base64," + new string('A', 120);
        _images.Setup(i => i.UploadAsync(3, img, default))
            .ReturnsAsync(new ProblemNoteImageUploadResult("https://res.cloudinary.com/demo/image/upload/v1/x.jpg", "yks/x"));

        await _service.AddAsync(3, img, new[] { "Matematik", "Türev" }, false);

        saved.Should().NotBeNull();
        saved!.UserId.Should().Be(3);
        saved.ImageUrl.Should().StartWith("https://res.cloudinary.com/");
        saved.ImagePublicId.Should().Be("yks/x");
        saved.TagsJson.ToLowerInvariant().Should().Contain("matematik");
        saved.SolutionLearned.Should().BeFalse();
    }
}
