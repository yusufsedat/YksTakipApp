using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints
{
    public static class StatsEndpoints
    {
        public static void MapStatsEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/stats")
                .RequireAuthorization();

            group.MapGet("/summary", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var summary = await statsService.GetSummaryAsync(userId.Value);
                return Results.Ok(summary);
            });

            group.MapGet("/weekly", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var weekly = await statsService.GetWeeklyAsync(userId.Value);
                return Results.Ok(weekly);
            });

            group.MapGet("/progress", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var progress = await statsService.GetProgressAsync(userId.Value);
                return Results.Ok(progress);
            });
        }
    }
}

