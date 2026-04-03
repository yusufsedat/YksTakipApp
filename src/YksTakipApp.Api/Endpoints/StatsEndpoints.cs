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
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetSummaryAsync(userId.Value));
            });

            group.MapGet("/weekly", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetWeeklyAsync(userId.Value));
            });

            group.MapGet("/progress", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetProgressAsync(userId.Value));
            });

            group.MapGet("/wins", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetWinsAsync(userId.Value));
            });

            var examStats = app.MapGroup("/exam/stats")
                .RequireAuthorization();

            examStats.MapGet("/tyt", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetTytStatsAsync(userId.Value));
            });

            examStats.MapGet("/ayt", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetAytStatsAsync(userId.Value));
            });

            examStats.MapGet("/brans", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetBransStatsAsync(userId.Value));
            });
        }
    }
}
