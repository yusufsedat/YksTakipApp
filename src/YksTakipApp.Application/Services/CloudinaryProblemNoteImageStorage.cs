using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YksTakipApp.Application.Options;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services;

public sealed class CloudinaryProblemNoteImageStorage : IProblemNoteImageStorage
{
    private readonly Cloudinary _cloudinary;
    private readonly CloudinarySettings _settings;
    private readonly ILogger<CloudinaryProblemNoteImageStorage> _logger;

    public CloudinaryProblemNoteImageStorage(
        IOptions<CloudinarySettings> options,
        ILogger<CloudinaryProblemNoteImageStorage> logger)
    {
        _settings = options.Value;
        _logger = logger;
        var account = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<ProblemNoteImageUploadResult> UploadAsync(int userId, string imageBase64OrDataUrl, CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
            throw new InvalidOperationException(
                "Cloudinary yapılandırılmadı. appsettings veya ortam: Cloudinary__CloudName, Cloudinary__ApiKey, Cloudinary__ApiSecret.");

        var (bytes, contentType) = ProblemNoteImagePayloadParser.Parse(imageBase64OrDataUrl);
        if (bytes.Length > 12 * 1024 * 1024)
            throw new InvalidOperationException("Görüntü çok büyük (en fazla ~12 MB).");

        await using var stream = new MemoryStream(bytes, writable: false);
        var folder = $"{_settings.Folder.TrimEnd('/')}/user_{userId}";
        var publicId = $"{Guid.NewGuid():N}";

        var ext = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription($"note.{ext}", stream),
            Folder = folder,
            PublicId = publicId,
            Overwrite = false,
            UseFilename = false,
            UniqueFilename = false,
            Invalidate = true,
        };

        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null)
        {
            _logger.LogError("Cloudinary yükleme hatası: {Message}", result.Error.Message);
            throw new InvalidOperationException($"Cloudinary: {result.Error.Message}");
        }

        var url = result.SecureUrl?.ToString();
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(result.PublicId))
            throw new InvalidOperationException("Cloudinary yanıtı eksik (SecureUrl / PublicId).");

        return new ProblemNoteImageUploadResult(url, result.PublicId);
    }

    public async Task DeleteAsync(string? imagePublicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePublicId) || imagePublicId.StartsWith("stub/", StringComparison.Ordinal))
            return;

        if (!_settings.IsConfigured)
            return;

        try
        {
            var del = await _cloudinary.DestroyAsync(new DeletionParams(imagePublicId) { ResourceType = ResourceType.Image });
            if (del.Error != null)
                _logger.LogWarning("Cloudinary silinemedi {PublicId}: {Message}", imagePublicId, del.Error.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cloudinary DeleteAsync hatası: {PublicId}", imagePublicId);
        }
    }
}
