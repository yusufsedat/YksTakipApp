using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints;

public static class RecommendationEndpoints
{
    public static void MapRecommendationEndpoints(this WebApplication app)
    {
        app.MapGet("/recommendations/today", async (
            IRecommendationService svc,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var list = await svc.GetDailyRecommendationsAsync(userId.Value, ct);
            return Results.Ok(list);
        })
        .RequireAuthorization()
        .WithTags("Recommendations")
        .WithSummary("Günlük öneri listesi (en fazla 5 konu)")
        .WithDescription("Durumsuz skor motoru; veritabanına yazmaz, yalnızca okuma yapar.");
    }
}
