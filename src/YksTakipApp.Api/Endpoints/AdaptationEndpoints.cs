using FluentValidation;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Api.Endpoints;

public static class AdaptationEndpoints
{
    public static void MapAdaptationEndpoints(this WebApplication app)
    {
        app.MapPost("/adaptation/evaluate-performance", async (
            EvaluatePerformanceRequest body,
            IValidator<EvaluatePerformanceRequest> validator,
            IAdaptationService adaptation,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var validation = await validator.ValidateAsync(body, ct);
            if (!validation.IsValid)
                return ctx.ValidationProblem(validation.ToDictionary());

            await adaptation.EvaluateTopicPerformanceAsync(userId.Value, body.TopicId, body.RecentExamScorePercent, ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .RequireRateLimiting("writes")
        .WithTags("Adaptation")
        .WithSummary("Konu performansını değerlendir (teşhis tetikleme)")
        .WithDescription("Düşük skor + LearnedExternally ön koşul varsa DiagnosticTest görevi planlar.");

        app.MapPost("/adaptation/diagnostic-tasks/{taskId:int}/result", async (
            int taskId,
            DiagnosticResultRequest body,
            IValidator<DiagnosticResultRequest> validator,
            IAdaptationService adaptation,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.GetUserId();
            if (userId is null)
                return Results.Unauthorized();

            var validation = await validator.ValidateAsync(body, ct);
            if (!validation.IsValid)
                return ctx.ValidationProblem(validation.ToDictionary());

            var dr = ParseDiagnosticResult(body.Result);
            var rec = await adaptation.RecordDiagnosticTestResultAsync(userId.Value, taskId, dr, ct);
            if (rec is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                outcome = rec.Outcome,
                task = ScheduleTaskMapping.ToDto(rec.ScheduleTask)
            });
        })
        .RequireAuthorization()
        .RequireRateLimiting("writes")
        .WithTags("Adaptation")
        .WithSummary("Teşhis görevi sonucu")
        .WithDescription("passed, failed veya skipped.");
    }

    private static DiagnosticResult ParseDiagnosticResult(string raw) =>
        raw.ToLowerInvariant() switch
        {
            "passed" => DiagnosticResult.Passed,
            "failed" => DiagnosticResult.Failed,
            _ => DiagnosticResult.SkippedOrDeleted
        };
}
