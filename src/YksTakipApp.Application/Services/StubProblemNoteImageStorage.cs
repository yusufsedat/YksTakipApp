using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services;

/// <summary>Integration test veya Cloudinary yapılandırması yokken (sadece Testing ortamı önerilir).</summary>
public sealed class StubProblemNoteImageStorage : IProblemNoteImageStorage
{
    public Task<ProblemNoteImageUploadResult> UploadAsync(int userId, string imageBase64OrDataUrl, CancellationToken cancellationToken = default)
    {
        var (bytes, _) = ProblemNoteImagePayloadParser.Parse(imageBase64OrDataUrl);
        var id = $"stub/user_{userId}/{Guid.NewGuid():N}";
        // Küçük veri URL — testlerde gerçek ağ yok
        var b64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:image/jpeg;base64,{b64}";
        return Task.FromResult(new ProblemNoteImageUploadResult(dataUrl, id));
    }

    public Task DeleteAsync(string? imagePublicId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
