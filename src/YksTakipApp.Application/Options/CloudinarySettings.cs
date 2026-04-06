namespace YksTakipApp.Application.Options;

public sealed class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    /// <summary>Klasör öneki, örn. yks_problem_notes</summary>
    public string Folder { get; set; } = "yks_problem_notes";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);
}
