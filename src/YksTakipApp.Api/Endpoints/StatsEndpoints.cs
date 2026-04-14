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
            })
            .WithTags("Stats")
            .WithSummary("Genel istatistik özeti")
            .WithDescription("Kullanıcının çalışma, konu ve sınav verilerinden üretilen genel özet metriklerini döndürür.");

            group.MapGet("/weekly", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetWeeklyAsync(userId.Value));
            })
            .WithTags("Stats")
            .WithSummary("Haftalık çalışma istatistikleri")
            .WithDescription("Son 7 güne ait çalışma sürelerini ve haftalık dağılım verilerini döndürür.");

            group.MapGet("/progress", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetProgressAsync(userId.Value));
            })
            .WithTags("Stats")
            .WithSummary("İlerleme istatistikleri")
            .WithDescription("Konu tamamlama ve zaman içindeki gelişim bilgilerini döndürür.");

            group.MapGet("/wins", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetWinsAsync(userId.Value));
            })
            .WithTags("Stats")
            .WithSummary("Küçük zaferler")
            .WithDescription("Kullanıcının motivasyon amaçlı kazanım/başarı özetlerini döndürür.");

            var examStats = app.MapGroup("/exam/stats")
                .RequireAuthorization();

            examStats.MapGet("/tyt", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetTytStatsAsync(userId.Value));
            })
            .WithTags("ExamStats")
            .WithSummary("TYT deneme istatistikleri")
            .WithDescription("Kullanıcının TYT deneme performans metriklerini döndürür.");

            examStats.MapGet("/ayt", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetAytStatsAsync(userId.Value));
            })
            .WithTags("ExamStats")
            .WithSummary("AYT deneme istatistikleri")
            .WithDescription("Kullanıcının AYT deneme performans metriklerini döndürür.");

            examStats.MapGet("/brans", async (IStatsService statsService, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null) return Results.Unauthorized();
                return Results.Ok(await statsService.GetBransStatsAsync(userId.Value));
            })
            .WithTags("ExamStats")
            .WithSummary("Branş deneme istatistikleri")
            .WithDescription("Kullanıcının branş bazlı deneme ve net dağılım analizlerini döndürür.");
        }
    }
}
