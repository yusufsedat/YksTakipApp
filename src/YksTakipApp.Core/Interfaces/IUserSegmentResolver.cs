namespace YksTakipApp.Core.Interfaces;

/// <summary>
/// Kullanıcının feature flag segmentini döner: null, "new_user", "active_user", "beta".
/// FeatureFlagService bu logic'i kendi içinde tutmaz; ayrı servistir.
/// </summary>
public interface IUserSegmentResolver
{
    Task<string?> ResolveAsync(int userId, CancellationToken ct = default);
}
