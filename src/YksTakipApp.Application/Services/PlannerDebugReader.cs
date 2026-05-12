using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class PlannerDebugReader : IPlannerDebugReader
{
    private const int MED = 30;

    private readonly AppDbContext _db;
    private readonly IUserSegmentResolver _segmentResolver;
    private readonly IFeatureFlagService _featureFlags;

    public PlannerDebugReader(AppDbContext db, IUserSegmentResolver segmentResolver, IFeatureFlagService featureFlags)
    {
        _db = db;
        _segmentResolver = segmentResolver;
        _featureFlags = featureFlags;
    }

    public async Task<PlannerDebugSnapshot?> GetAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var activeGoal = user.ActiveGoalVersionId is null ? null :
            await _db.UserGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == user.ActiveGoalVersionId.Value, ct);

        // Capacity hesabı: dynamic buffer ve multiplier için executionRatio'ya ihtiyaç var.
        // Burada DynamicPlannerService'in kendi içindeki davranışıyla aynı kurali tekrar etmemek için
        // yaklaşık değer döner: son 7 günde planned vs completed oranı.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-7);

        var (planned, completed) = await GetExecutionStatsAsync(userId, weekStart, today, ct);
        var executionRatio = planned == 0 ? 1.0 : completed / (double)planned;
        var multiplier = DynamicPlannerService.CalculateCapacityMultiplier(executionRatio);
        var bufferRate = DynamicPlannerService.CalculateDynamicBufferRate(executionRatio);

        var dailyCap = activeGoal?.DailyAvailableMinutes ?? 0;
        var effectiveDaily = (int)Math.Round(dailyCap * multiplier, MidpointRounding.AwayFromZero);
        var workingDaily = (int)Math.Floor(Math.Max(MED, effectiveDaily) * (1 - bufferRate));
        if (dailyCap == 0)
            workingDaily = 0;

        var weekMonday = StartOfWeekMonday(today);
        var weekSunday = weekMonday.AddDays(6);
        var latestPlan = await _db.ScheduleTasks.AsNoTracking()
            .Where(t => t.UserId == userId && t.TaskDate >= weekMonday && t.TaskDate <= weekSunday)
            .OrderBy(t => t.TaskDate)
            .Select(t => new PlannerDebugTaskDto(
                t.Id, t.TopicId, t.Topic == null ? null : t.Topic.Name, t.TaskDate, t.DurationMinutes,
                t.Status.ToString(), t.TaskType.ToString()))
            .ToListAsync(ct);

        var latestLog = await _db.PlannerDecisionLogs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PlannerDebugDecisionLogDto(
                x.Id, x.WeekStart, x.Status, x.ReasonCode,
                x.TaskCountTotal, x.QualityScore, x.DurationMs, x.CreatedAt, x.BreakdownJson))
            .FirstOrDefaultAsync(ct);

        var priorityRequests = await _db.UserTopics.AsNoTracking()
            .Where(ut => ut.UserId == userId && ut.IsPriorityRequested)
            .Select(ut => new PlannerDebugPriorityRequestDto(
                ut.TopicId,
                ut.Topic == null ? null : ut.Topic.Name,
                ut.PriorityRequestedAt,
                ut.PriorityExpiresAt,
                ut.PriorityResolvedAt))
            .ToListAsync(ct);

        var churn = await _db.UserPlannerChurnEvents.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(5)
            .Select(e => new PlannerDebugChurnEventDto(e.Id, e.TriggerDate, (int)e.ReasonCode, e.CreatedAt))
            .ToListAsync(ct);

        var segment = await _segmentResolver.ResolveAsync(userId, ct);

        var flagKeys = await _db.FeatureFlags.AsNoTracking().Select(f => f.Key).ToListAsync(ct);
        var flagMap = new Dictionary<string, bool>();
        foreach (var key in flagKeys)
            flagMap[key] = await _featureFlags.IsEnabledAsync(key, userId, ct);

        return new PlannerDebugSnapshot
        {
            User = new PlannerDebugUserDto(user.Id, user.Name, user.Role, user.ActiveGoalVersionId, user.CreatedAt),
            ActiveGoal = activeGoal is null ? null :
                new PlannerDebugGoalDto(activeGoal.Id, activeGoal.DailyAvailableMinutes, activeGoal.TargetTytNet, activeGoal.TargetAytNet, activeGoal.CreatedAt),
            Capacity = new PlannerDebugCapacityDto
            {
                DailyAvailableMinutes = dailyCap,
                EffectiveCapacityMultiplier = multiplier,
                DynamicBufferRate = bufferRate,
                WorkingDaily = workingDaily
            },
            Segment = segment,
            LatestPlan = latestPlan,
            LatestDecisionLog = latestLog,
            PriorityRequests = priorityRequests,
            RecentChurnEvents = churn,
            FeatureFlags = flagMap
        };
    }

    private async Task<(int planned, int completed)> GetExecutionStatsAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var stats = await _db.ScheduleTasks.AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.TaskDate >= from && t.TaskDate <= to
                        && t.TaskType == TaskType.Study)
            .GroupBy(t => 1)
            .Select(g => new
            {
                Planned = g.Count(),
                Completed = g.Count(t => t.Status == ScheduleTaskStatus.Completed)
            })
            .FirstOrDefaultAsync(ct);
        return stats is null ? (0, 0) : (stats.Planned, stats.Completed);
    }

    private static DateOnly StartOfWeekMonday(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        var offset = dow == 0 ? -6 : 1 - dow;
        return date.AddDays(offset);
    }
}
