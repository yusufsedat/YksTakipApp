using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Application.Services;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapPost("/notifications/preview", async (
            INotificationPolicyService policy,
            AppDbContext db,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var day = DateOnly.FromDateTime(DateTime.UtcNow);
            var payloads = await policy.PreviewDailyNotificationsAsync(userId.Value, day, ct);
            foreach (var payload in payloads)
            {
                db.UserNotificationLogs.Add(new UserNotificationLog
                {
                    UserId = userId.Value,
                    NotificationType = payload.Type,
                    Message = payload.Message,
                    PayloadJson = NotificationPolicyService.SerializePayload(payload),
                    TargetDate = day
                });
            }

            if (payloads.Count > 0)
                await db.SaveChangesAsync(ct);

            return Results.Ok(payloads);
        })
        .RequireAuthorization()
        .RequireRateLimiting("writes")
        .WithTags("Notifications")
        .WithSummary("Bildirim karar onizlemesi")
        .WithDescription("Kapasite ve motivasyon tabanli ayni gun icin tekil bildirim payload listesini dondurur.");
    }
}
