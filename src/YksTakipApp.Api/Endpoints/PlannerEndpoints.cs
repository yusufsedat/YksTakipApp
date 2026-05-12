using FluentValidation;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace YksTakipApp.Api.Endpoints;

public static class PlannerEndpoints
{
    public static void MapPlannerEndpoints(this WebApplication app)
    {
        app.MapPost("/planner/generate", async (
            GenerateWeeklyPlanRequest body,
            IValidator<GenerateWeeklyPlanRequest> validator,
            IDynamicPlannerService planner,
            IIdempotentCommandExecutor idempotent,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();
            using var _ = RequestContextHelper.PushOperationContext(ctx, "Planner.Generate", userId, idempotencyKey: null);
            var log = loggerFactory.CreateLogger("PlannerEndpoints");

            var validation = await validator.ValidateAsync(body, ct);
            if (!validation.IsValid)
                return ctx.ValidationProblem(validation.ToDictionary());

            var idempotencyKey = RequestContextHelper.ResolveIdempotencyKey(ctx, body.ClientRequestId);

            var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? ctx.TraceIdentifier;
            var metadata = new PlannerCallMetadata(correlationId, idempotencyKey);

            return await idempotent.ExecuteAsync(
                ctx,
                userId.Value,
                "planner.generate",
                idempotencyKey,
                async token =>
                {
                    var result = await planner.GenerateWeeklyPlanAsync(userId.Value, body.StartDate, metadata, token);
                    var response = ScheduleTaskMapping.ToResponse(result);
                    log.LogInformation("Planner generate finished with {Result}.",
                        result.Status == PlanGenerationStatus.Success ? "success" : result.ReasonCode.ToString());
                    return response;
                },
                response => response.ReasonCode == PlanGenerationReasonCode.RequiresGoal
                    ? Results.UnprocessableEntity(response)
                    : Results.Ok(response),
                log,
                ct);
        })
        .RequireAuthorization()
        .RequireRateLimiting("writes")
        .WithTags("Planner")
        .WithSummary("Haftalık dinamik plan üret")
        .WithDescription("Seçilen haftanın Planned görevlerini siler ve öneri motoruna göre yeni plan yazar.");

        app.MapGet("/planner/weekly", async (
            DateOnly? start,
            DateOnly? end,
            IValidator<PlannerWeeklyQuery> validator,
            IDynamicPlannerService planner,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();
            using var _ = RequestContextHelper.PushOperationContext(ctx, "Planner.Weekly", userId, idempotencyKey: null);
            var log = loggerFactory.CreateLogger("PlannerEndpoints");

            if (start is null || end is null)
                return Results.BadRequest(new { message = "start ve end sorgu parametreleri zorunludur (YYYY-MM-DD)." });

            var q = new PlannerWeeklyQuery { Start = start.Value, End = end.Value };
            var validation = await validator.ValidateAsync(q, ct);
            if (!validation.IsValid)
                return ctx.ValidationProblem(validation.ToDictionary());

            await planner.CheckAndTriggerChurnAsync(userId.Value, q.Start, q.End, ct);
            var list = await planner.GetWeeklyTasksAsync(userId.Value, q.Start, q.End, ct);
            log.LogInformation("Planner weekly fetch finished with {Result}.", "success");
            return Results.Ok(list.Select(ScheduleTaskMapping.ToDto).ToList());
        })
        .RequireAuthorization()
        .WithTags("Planner")
        .WithSummary("Tarih aralığındaki plan görevleri")
        .WithDescription("Kullanıcının ScheduleTask kayıtlarını döner (en fazla 14 günlük aralık).");

        app.MapPatch("/planner/tasks/{taskId:int}/status", async (
            int taskId,
            UpdateScheduleTaskStatusRequest body,
            IValidator<UpdateScheduleTaskStatusRequest> validator,
            IDynamicPlannerService planner,
            HttpContext ctx,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();
            using var _ = RequestContextHelper.PushOperationContext(ctx, "Planner.UpdateTaskStatus", userId, idempotencyKey: null);
            var log = loggerFactory.CreateLogger("PlannerEndpoints");

            var validation = await validator.ValidateAsync(body, ct);
            if (!validation.IsValid)
                return ctx.ValidationProblem(validation.ToDictionary());

            var updated = await planner.UpdateStatusAsync(userId.Value, taskId, body.Status, ct);
            if (updated is null)
            {
                log.LogWarning("Planner task status update returned {Result}.", "not_found");
                return Results.NotFound();
            }

            log.LogInformation("Planner task status update finished with {Result}.", "success");
            return Results.Ok(ScheduleTaskMapping.ToDto(updated));
        })
        .RequireAuthorization()
        .RequireRateLimiting("writes")
        .WithTags("Planner")
        .WithSummary("Görev durumu güncelle")
        .WithDescription("Completed, Skipped, Deferred veya Planned.");
    }

}
