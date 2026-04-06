namespace YksTakipApp.Core.Interfaces;

/// <summary>
/// Soru notu görselini harici depoda (Cloudinary) saklar; DB'de yalnızca URL + public id tutulur.
/// </summary>
public interface IProblemNoteImageStorage
{
    /// <summary>Ham veya data: URL base64 görüntüyü yükler.</summary>
    Task<ProblemNoteImageUploadResult> UploadAsync(int userId, string imageBase64OrDataUrl, CancellationToken cancellationToken = default);

    /// <summary>Cloudinary public id biliniyorsa görseli siler (not silinince / görsel değişince).</summary>
    Task DeleteAsync(string? imagePublicId, CancellationToken cancellationToken = default);
}

public sealed record ProblemNoteImageUploadResult(string SecureUrl, string PublicId);
