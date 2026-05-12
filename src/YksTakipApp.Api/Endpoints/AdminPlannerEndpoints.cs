using Microsoft.EntityFrameworkCore;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Endpoints;

public static class AdminPlannerEndpoints
{
    private const int MaxTake = 100;
    private const int MaxSkip = 10000;

    public static void MapAdminPlannerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/planner")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin/Planner");

        group.MapGet("/decision-logs", async Task<IResult> (
            int? userId,
            DateOnly? from,
            DateOnly? to,
            PlanGenerationReasonCode? reasonCode,
            int? take,
            int? skip,
            AppDbContext db,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var t = take ?? 50;
            var s = skip ?? 0;
            if (t < 1 || t > MaxTake)
                return ctx.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["take"] = new[] { $"take must be in [1, {MaxTake}]." }
                });
            if (s < 0 || s > MaxSkip)
                return ctx.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["skip"] = new[] { $"skip must be in [0, {MaxSkip}]." }
                });

            var q = db.PlannerDecisionLogs.AsNoTracking().AsQueryable();
            if (userId is not null)
                q = q.Where(x => x.UserId == userId.Value);
            if (from is not null)
                q = q.Where(x => x.WeekStart >= from.Value);
            if (to is not null)
                q = q.Where(x => x.WeekStart <= to.Value);
            if (reasonCode is not null)
                q = q.Where(x => x.ReasonCode == reasonCode.Value);

            var rows = await q
                .OrderByDescending(x => x.CreatedAt)
                .Skip(s)
                .Take(t)
                .Select(x => new PlannerDecisionLogSummaryDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    WeekStart = x.WeekStart,
                    WeekEnd = x.WeekEnd,
                    Status = x.Status,
                    ReasonCode = x.ReasonCode,
                    TaskCountTotal = x.TaskCountTotal,
                    QualityScore = x.QualityScore,
                    QualityBand = x.QualityBand,
                    DurationMs = x.DurationMs,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        })
        .WithSummary("Planner karar loglarını listele (sayfalı)");

        group.MapGet("/decision-logs/{id:long}", async Task<IResult> (
            long id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var row = await db.PlannerDecisionLogs
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new PlannerDecisionLogDetailDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    WeekStart = x.WeekStart,
                    WeekEnd = x.WeekEnd,
                    Status = x.Status,
                    ReasonCode = x.ReasonCode,
                    TaskCountTotal = x.TaskCountTotal,
                    TaskCountStudy = x.TaskCountStudy,
                    TaskCountReview = x.TaskCountReview,
                    TaskCountDiagnostic = x.TaskCountDiagnostic,
                    PreservedTaskCount = x.PreservedTaskCount,
                    RecommendationCandidateCount = x.RecommendationCandidateCount,
                    RecommendationScheduledCount = x.RecommendationScheduledCount,
                    RecommendationSkippedByCapacityCount = x.RecommendationSkippedByCapacityCount,
                    RecommendationSkippedByDuplicateCount = x.RecommendationSkippedByDuplicateCount,
                    DailyCapacity = x.DailyCapacity,
                    WorkingDaily = x.WorkingDaily,
                    BufferDaily = x.BufferDaily,
                    EffectiveCapacityMultiplier = x.EffectiveCapacityMultiplier,
                    DynamicBufferRate = x.DynamicBufferRate,
                    PriorityActiveCount = x.PriorityActiveCount,
                    PriorityPlacedCount = x.PriorityPlacedCount,
                    InjectedReviewTaskCount = x.InjectedReviewTaskCount,
                    QualityScore = x.QualityScore,
                    QualityBand = x.QualityBand,
                    BreakdownJson = x.BreakdownJson,
                    CorrelationId = x.CorrelationId,
                    IdempotencyKey = x.IdempotencyKey,
                    DurationMs = x.DurationMs,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync(ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        })
        .WithSummary("Planner karar logu detayı (BreakdownJson dahil)");

        group.MapGet("/decision-logs/stats", async Task<IResult> (
            DateTime? from,
            DateTime? to,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var q = db.PlannerDecisionLogs.AsNoTracking().AsQueryable();
            if (from is not null) q = q.Where(x => x.CreatedAt >= from.Value);
            if (to is not null) q = q.Where(x => x.CreatedAt <= to.Value);

            // Toplu kayit sayisi sinirli (admin debug); minimal projeksiyonla materialize edip in-memory agg.
            var rows = await q
                .Select(x => new
                {
                    x.Status,
                    x.ReasonCode,
                    x.QualityScore,
                    x.QualityBand,
                    x.PriorityActiveCount,
                    x.PriorityPlacedCount
                })
                .ToListAsync(ct);

            var total = rows.Count;
            var success = rows.Count(r => r.Status == PlanGenerationStatus.Success);
            var noPlan = rows.Count(r => r.Status == PlanGenerationStatus.NoPlanGenerated);
            var qualityRows = rows.Where(r => r.QualityScore != null).ToList();
            double? avgQuality = qualityRows.Count == 0 ? null : qualityRows.Average(r => (double)r.QualityScore!.Value);

            var dist = new QualityBandDistributionDto
            {
                Healthy = rows.Count(r => r.QualityBand == PlanQualityBand.Healthy),
                Warning = rows.Count(r => r.QualityBand == PlanQualityBand.Warning),
                Risky = rows.Count(r => r.QualityBand == PlanQualityBand.Risky)
            };

            var reasons = rows
                .Where(r => r.Status == PlanGenerationStatus.NoPlanGenerated)
                .GroupBy(r => r.ReasonCode)
                .Select(g => new NoPlanReasonStatDto(g.Key, g.Count()))
                .OrderByDescending(r => r.Count)
                .ToList();

            var prioritySamples = rows
                .Where(r => r.Status == PlanGenerationStatus.Success && r.PriorityActiveCount > 0)
                .ToList();
            double? priorityRate = prioritySamples.Count == 0
                ? null
                : prioritySamples.Average(s => s.PriorityPlacedCount / (double)s.PriorityActiveCount);
            var unplacedCount = prioritySamples.Count(s => s.PriorityPlacedCount < s.PriorityActiveCount);

            var dto = new PlannerDecisionStatsDto
            {
                WindowFromUtc = from,
                WindowToUtc = to,
                TotalCalls = total,
                SuccessCount = success,
                NoPlanCount = noPlan,
                AvgQualityScore = avgQuality,
                QualityBandDistribution = dist,
                TopNoPlanReasons = reasons,
                PriorityFulfillmentRate = priorityRate,
                CallsWithUnplacedPriority = unplacedCount
            };
            return Results.Ok(dto);
        })
        .WithSummary("Karar logu agregat özeti (Faz 7 hazırlık kontrol listesi)");

        app.MapGet("/admin/users/{userId:int}/planner-debug", async Task<IResult> (
            int userId,
            IPlannerDebugReader reader,
            CancellationToken ct) =>
        {
            var snapshot = await reader.GetAsync(userId, ct);
            return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
        })
        .RequireAuthorization("AdminOnly")
        .WithTags("Admin/Planner")
        .WithSummary("Kullanıcının planner debug aggregate snapshot'ı");
    }
}
