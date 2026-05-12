using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db) => _db = db;

    public async Task<ChurnSummaryDto> GetChurnSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var events = await _db.UserPlannerChurnEvents.AsNoTracking()
            .Where(e => e.TriggerDate >= from && e.TriggerDate <= to)
            .ToListAsync(ct);
        var triggerCount = events.Count;
        var churnedUserCount = events.Select(e => e.UserId).Distinct().Count();
        var avgLatency = events.Where(e => e.DaysSincePlanGenerated.HasValue)
            .Select(e => (double)e.DaysSincePlanGenerated!.Value)
            .DefaultIfEmpty(0)
            .Average();

        var trend7Start = to.AddDays(-6);
        var trend14Start = to.AddDays(-13);
        var trend7 = events.Count(e => e.TriggerDate >= trend7Start && e.TriggerDate <= to);
        var trend14 = events.Count(e => e.TriggerDate >= trend14Start && e.TriggerDate <= to);

        var userIds = events.Select(e => e.UserId).Distinct().ToList();
        var newUserIds = await _db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.CreatedAt >= to.AddDays(-14).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Select(u => u.Id)
            .ToListAsync(ct);
        var activeUserIds = await _db.ScheduleTasks.AsNoTracking()
            .Where(t => userIds.Contains(t.UserId)
                        && t.Status == Core.Enums.ScheduleTaskStatus.Completed
                        && t.TaskDate >= to.AddDays(-6)
                        && t.TaskDate <= to)
            .Select(t => t.UserId)
            .Distinct()
            .ToListAsync(ct);

        return new ChurnSummaryDto(
            triggerCount,
            churnedUserCount,
            Math.Round(avgLatency, 2),
            trend7,
            trend14,
            newUserIds.Count,
            activeUserIds.Count);
    }

    public async Task<FeedbackLoopSummaryDto> GetFeedbackSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var userIds = await _db.ScheduleTasks.AsNoTracking()
            .Where(t => t.TaskDate >= from && t.TaskDate <= to)
            .Select(t => t.UserId)
            .Distinct()
            .ToListAsync(ct);
        var all = new List<FeedbackLoopUserDto>(userIds.Count);
        foreach (var userId in userIds)
            all.Add(await GetFeedbackForUserAsync(userId, from, to, ct));

        return new FeedbackLoopSummaryDto(
            from,
            to,
            all.Count,
            all.Count == 0 ? 0 : Math.Round(all.Average(x => x.DifficultyScore), 2),
            all.Count == 0 ? 0 : Math.Round(all.Average(x => x.SatisfactionScore), 2),
            all.Count(x => x.DifficultyScore >= 70),
            all.Count(x => x.IsNewUserSegment),
            all.Count(x => x.IsActiveUserSegment));
    }

    public async Task<FeedbackLoopUserDto> GetFeedbackForUserAsync(int userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var tasks = await _db.ScheduleTasks.AsNoTracking()
            .Where(t => t.UserId == userId && t.TaskDate >= from && t.TaskDate <= to)
            .ToListAsync(ct);
        var total = Math.Max(1, tasks.Count);
        var completed = tasks.Count(t => t.Status == Core.Enums.ScheduleTaskStatus.Completed);
        var deferred = tasks.Count(t => t.Status == Core.Enums.ScheduleTaskStatus.Deferred);
        var skipped = tasks.Count(t => t.Status == Core.Enums.ScheduleTaskStatus.Skipped);
        var priorityRequestCount = await _db.UserTopics.AsNoTracking()
            .CountAsync(ut => ut.UserId == userId && ut.PriorityRequestedAt != null && ut.PriorityRequestedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && ut.PriorityRequestedAt <= to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), ct);
        var manualRegenerate = await _db.CommandExecutions.AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.Operation == "planner.generate" && c.CreatedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) && c.CreatedAt <= to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), ct);

        var completionRate = completed / (double)total;
        var deferRate = deferred / (double)total;
        var skipRate = skipped / (double)total;
        var priorityOverrideRate = priorityRequestCount / (double)total;

        var difficulty = Clamp100(100 * (0.40 * deferRate + 0.35 * skipRate + 0.20 * priorityOverrideRate - 0.15 * completionRate));
        var satisfaction = Clamp100(100 - difficulty + 15 * completionRate);
        var reason = difficulty >= 70
            ? "HighDeferral"
            : completionRate < 0.4
                ? "LowCompletion"
                : priorityOverrideRate > 0.2
                    ? "FrequentPriorityOverride"
                    : "Balanced";

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        var isNew = user is not null && user.CreatedAt >= to.AddDays(-14).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var completed7d = tasks.Count(t => t.Status == Core.Enums.ScheduleTaskStatus.Completed && t.TaskDate >= to.AddDays(-6));
        var isActive = completed7d >= 3;

        return new FeedbackLoopUserDto(
            userId,
            from,
            to,
            completed,
            deferred,
            skipped,
            priorityRequestCount,
            manualRegenerate,
            Math.Round(difficulty, 2),
            Math.Round(satisfaction, 2),
            reason,
            isNew,
            isActive);
    }

    private static double Clamp100(double value) => Math.Max(0, Math.Min(100, value));
}
