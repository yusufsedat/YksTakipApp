using YksTakipApp.Api.Models;

namespace YksTakipApp.Api.Services;

public interface IAppVersionService
{
    AppVersion? GetByPlatform(string platform);
}

public sealed class AppVersionService(IConfiguration configuration) : IAppVersionService
{
    private readonly IConfiguration _configuration = configuration;

    public AppVersion? GetByPlatform(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform)) return null;
        var normalized = platform.Trim().ToLowerInvariant();
        if (normalized is not ("android" or "ios")) return null;

        var section = _configuration.GetSection($"AppVersion:{normalized}");
        if (!section.Exists()) return null;

        var item = new AppVersion
        {
            Platform = normalized,
            MinimumVersion = section["MinimumVersion"] ?? "",
            LatestVersion = section["LatestVersion"] ?? "",
            StoreUrl = section["StoreUrl"] ?? ""
        };

        if (string.IsNullOrWhiteSpace(item.MinimumVersion)
            || string.IsNullOrWhiteSpace(item.LatestVersion)
            || string.IsNullOrWhiteSpace(item.StoreUrl))
            return null;

        return item;
    }
}
