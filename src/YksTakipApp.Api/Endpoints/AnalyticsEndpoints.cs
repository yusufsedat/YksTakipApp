using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        app.MapGet("/analytics/churn/summary", async (
            DateOnly? from,
            DateOnly? to,
            IAnalyticsService analytics,
            CancellationToken ct) =>
        {
            var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = from ?? end.AddDays(-13);
            if (start > end)
                return Results.BadRequest(new { message = "from to'dan buyuk olamaz." });
            var data = await analytics.GetChurnSummaryAsync(start, end, ct);
            return Results.Ok(data);
        })
        .RequireAuthorization()
        .WithTags("Analytics")
        .WithSummary("Churn analitik özeti");

        app.MapGet("/analytics/feedback-loop/summary", async (
            DateOnly? from,
            DateOnly? to,
            IAnalyticsService analytics,
            CancellationToken ct) =>
        {
            var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = from ?? end.AddDays(-13);
            if (start > end)
                return Results.BadRequest(new { message = "from to'dan buyuk olamaz." });
            var data = await analytics.GetFeedbackSummaryAsync(start, end, ct);
            return Results.Ok(data);
        })
        .RequireAuthorization()
        .WithTags("Analytics")
        .WithSummary("Feedback-loop özet metriği");

        app.MapGet("/analytics/feedback-loop/user/{userId:int}", async (
            int userId,
            DateOnly? from,
            DateOnly? to,
            IAnalyticsService analytics,
            CancellationToken ct) =>
        {
            var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var start = from ?? end.AddDays(-13);
            if (start > end)
                return Results.BadRequest(new { message = "from to'dan buyuk olamaz." });
            var data = await analytics.GetFeedbackForUserAsync(userId, start, end, ct);
            return Results.Ok(data);
        })
        .RequireAuthorization()
        .WithTags("Analytics")
        .WithSummary("Kullanıcı feedback-loop metriği");
    }
}
