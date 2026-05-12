namespace YksTakipApp.Core.Entities;

/// <summary>
/// Kullanıcı bazlı flag override. (UserId, FlagKey) unique. ExpiresAt geçtiyse ignore.
/// </summary>
public sealed class UserFeatureFlagOverride
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string FlagKey { get; set; } = "";
    public bool IsEnabled { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
