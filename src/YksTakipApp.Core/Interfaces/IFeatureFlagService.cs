namespace YksTakipApp.Core.Interfaces;

/// <summary>
/// Feature flag değerlendirme servisi. Öncelik (userId != null için):
/// user override (varsa, expired değilse) > segment match > rollout hash > global IsEnabled.
/// userId == null ise sadece global IsEnabled okunur.
/// </summary>
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string key, int? userId, CancellationToken ct = default);
}
