using FluentValidation;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Api.Endpoints
{
    public static class GoalEndpoints
    {
        public static void MapGoalEndpoints(this WebApplication app)
        {
            app.MapGet("/users/goals/status", async (IGoalService goals, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var status = await goals.GetStatusAsync(userId.Value);
                if (status is null)
                    return Results.Unauthorized();

                return Results.Ok(ToResponse(status));
            })
            .RequireAuthorization()
            .WithTags("Goals")
            .WithSummary("Hedef onboarding durumu")
            .WithDescription("Aktif hedef, skip hakkı ve mevcut hedef bilgisini döndürür.");

            app.MapPost("/users/goals", async (
                CreateUserGoalRequest req,
                IValidator<CreateUserGoalRequest> validator,
                IGoalService goals,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                try
                {
                    var created = await goals.CreateAsync(
                        userId.Value,
                        req.TargetUniversity.Trim(),
                        req.TargetDepartment.Trim(),
                        req.TargetTytNet,
                        req.TargetAytNet,
                        req.DailyAvailableMinutes);
                    return Results.Ok(ToDto(created));
                }
                catch (InvalidOperationException ex) when (ex.Message == GoalService.UserNotFoundMessage)
                {
                    return Results.Unauthorized();
                }
            })
            .RequireAuthorization()
            .RequireRateLimiting("writes")
            .WithTags("Goals")
            .WithSummary("Yeni hedef kaydı")
            .WithDescription("Immutable hedef geçmişine yeni satır ekler ve aktif sürümü günceller.");

            app.MapPost("/users/goals/skip", async (IGoalService goals, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    var skipCount = await goals.SkipAsync(userId.Value);
                    return Results.Ok(new SkipGoalResponse { SkipCount = skipCount });
                }
                catch (InvalidOperationException ex) when (ex.Message == GoalService.SkipLimitMessage)
                {
                    return Results.Json(
                        new { message = "Skip hakkınız dolmuştur." },
                        statusCode: StatusCodes.Status403Forbidden);
                }
                catch (InvalidOperationException ex) when (ex.Message == GoalService.ActiveGoalExistsMessage)
                {
                    return Results.Conflict(new { message = "Aktif hedefiniz varken skip kullanılamaz." });
                }
                catch (InvalidOperationException ex) when (ex.Message == GoalService.UserNotFoundMessage)
                {
                    return Results.Unauthorized();
                }
            })
            .RequireAuthorization()
            .RequireRateLimiting("writes")
            .WithTags("Goals")
            .WithSummary("Akıllı onboarding atla")
            .WithDescription("Skip sayacını artırır; limit ve aktif hedef kurallarına tabidir.");
        }

        private static GoalStatusResponse ToResponse(GoalStatusResult r) =>
            new()
            {
                HasActiveGoal = r.HasActiveGoal,
                CanSkip = r.CanSkip,
                CurrentGoal = r.CurrentGoal is null ? null : ToDto(r.CurrentGoal)
            };

        private static UserGoalDto ToDto(UserGoalSnapshot s) =>
            new()
            {
                Id = s.Id,
                TargetUniversity = s.TargetUniversity,
                TargetDepartment = s.TargetDepartment,
                TargetTytNet = s.TargetTytNet,
                TargetAytNet = s.TargetAytNet,
                DailyAvailableMinutes = s.DailyAvailableMinutes,
                CreatedAt = s.CreatedAt
            };
    }
}
