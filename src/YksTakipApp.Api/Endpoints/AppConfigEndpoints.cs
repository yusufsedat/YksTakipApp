using YksTakipApp.Api.Services;

namespace YksTakipApp.Api.Endpoints;

public static class AppConfigEndpoints
{
    public static void MapAppConfigEndpoints(this WebApplication app)
    {
        app.MapGet("/api/app-config/check-version", (string platform, IAppVersionService svc) =>
        {
            var item = svc.GetByPlatform(platform);
            if (item is null)
            {
                return Results.NotFound(new
                {
                    message = "Platform için versiyon yapılandırması bulunamadı."
                });
            }

            return Results.Ok(item);
        })
        .WithTags("AppConfig")
        .WithSummary("Uygulama sürüm kontrolü")
        .WithDescription("Belirtilen platform (ios/android) için minimum sürüm ve güncelleme gereksinimi bilgisini döndürür.");
    }
}
