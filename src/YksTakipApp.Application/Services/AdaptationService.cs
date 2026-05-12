using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YksTakipApp.Application.Options;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class AdaptationService : IAdaptationService
{
    public const string DefaultDiagnosticReason =
        "Son denemelerde bu konuda zorlandığını fark ettik. Temelde bir eksik kalıp kalmadığını görmek için bu kısa tekrar testini çözelim.";

    private const int DiagnosticDurationMinutes = 20;
    private const int DiagnosticSkipGuardWindow = 5;
    private const double PassedConfidenceFloor = 0.85;
    private const double LearnedExternallyConfidence = 0.55;

    private readonly AppDbContext _db;
    private readonly ILogger<AdaptationService> _log;
    private readonly AdaptationPolicyOptions _policy;

    public AdaptationService(AppDbContext db, ILogger<AdaptationService> log, IOptions<AdaptationPolicyOptions> policy)
    {
        _db = db;
        _log = log;
        _policy = policy.Value;
    }

    public async Task EvaluateTopicPerformanceAsync(int userId, int topicId, int recentExamScorePercent, CancellationToken ct = default)
    {
        var isLow = recentExamScorePercent < _policy.LowScoreThresholdPercent;
        var isHigh = recentExamScorePercent >= _policy.HighScoreThresholdPercent;
        if (!isLow && !isHigh)
            return;

        var now = DateTime.UtcNow;
        var mainUt = await _db.UserTopics.FirstOrDefaultAsync(
            ut => ut.UserId == userId && ut.TopicId == topicId,
            ct);
        if (mainUt is not null)
        {
            if (isLow)
            {
                mainUt.MasteryStatus = MasteryStatus.NeedsReview;
                mainUt.MasteryConfidence = Math.Max(0.0, mainUt.MasteryConfidence - _policy.LowScoreConfidencePenalty);
                if (mainUt.MasteryConfidence <= _policy.LockThreshold)
                    mainUt.IsLocked = true;
            }
            else
            {
                mainUt.MasteryConfidence = Math.Min(1.0, mainUt.MasteryConfidence + _policy.HighScoreConfidenceGain);
                if (mainUt.MasteryConfidence >= _policy.UnlockThreshold)
                {
                    mainUt.IsLocked = false;
                    mainUt.MasteryStatus = MasteryStatus.Mastered;
                }
            }

            mainUt.LastEvaluatedAt = now;
        }

        if (!isLow)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation(
                "Adaptation high-performance update applied for user {UserId}, topic {TopicId}, score {ScorePercent}.",
                userId, topicId, recentExamScorePercent);
            return;
        }

        var prereqIds = await _db.TopicPrerequisites
            .AsNoTracking()
            .Where(p => p.TopicId == topicId)
            .Select(p => p.PrerequisiteTopicId)
            .Distinct()
            .ToListAsync(ct);

        if (prereqIds.Count == 0)
            return;

        var userTopics = await _db.UserTopics
            .Where(ut => ut.UserId == userId && prereqIds.Contains(ut.TopicId))
            .ToListAsync(ct);

        var suspiciousPrereqIds = userTopics
            .Where(ut => ut.MasteryStatus == MasteryStatus.LearnedExternally)
            .Select(ut => ut.TopicId)
            .ToList();

        if (suspiciousPrereqIds.Count == 0)
        {
            _log.LogInformation(
                "Adaptation evaluate: user {UserId} topic {TopicId} low score but no LearnedExternally prerequisite.",
                userId, topicId);
            return;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var tomorrow = DateOnly.FromDateTime(now).AddDays(1);

            foreach (var prereqTopicId in suspiciousPrereqIds)
            {
                var shouldThrottle = await ShouldThrottleDiagnosticAsync(userId, prereqTopicId, topicId, ct);
                if (shouldThrottle)
                {
                    _log.LogWarning(
                        "Adaptation evaluate throttled diagnostic creation for user {UserId}, prereq {PrereqTopicId}, main {MainTopicId}.",
                        userId,
                        prereqTopicId,
                        topicId);
                    continue;
                }

                var duplicate = await _db.ScheduleTasks.AnyAsync(
                    t => t.UserId == userId
                         && t.TaskType == TaskType.DiagnosticTest
                         && t.Status == ScheduleTaskStatus.Planned
                         && t.PrerequisiteTopicId == prereqTopicId
                         && t.MainTopicId == topicId,
                    ct);

                if (duplicate)
                    continue;

                _db.ScheduleTasks.Add(new ScheduleTask
                {
                    UserId = userId,
                    TopicId = prereqTopicId,
                    TaskDate = tomorrow,
                    DurationMinutes = DiagnosticDurationMinutes,
                    Status = ScheduleTaskStatus.Planned,
                    IsRecoveryTask = true,
                    TaskType = TaskType.DiagnosticTest,
                    Reason = DefaultDiagnosticReason,
                    PrerequisiteTopicId = prereqTopicId,
                    MainTopicId = topicId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<AdaptationRecordResult?> RecordDiagnosticTestResultAsync(
        int userId,
        int taskId,
        DiagnosticResult result,
        CancellationToken ct = default)
    {
        var task = await _db.ScheduleTasks
            .Include(t => t.Topic)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);

        if (task is null)
            return null;

        if (task.TaskType != TaskType.DiagnosticTest)
        {
            _log.LogWarning("RecordDiagnostic: task {TaskId} is not DiagnosticTest.", taskId);
            return null;
        }

        if (task.PrerequisiteTopicId is not { } prereqTopicId)
        {
            _log.LogWarning("RecordDiagnostic: task {TaskId} missing PrerequisiteTopicId.", taskId);
            return null;
        }

        if (task.Status is ScheduleTaskStatus.Completed or ScheduleTaskStatus.Skipped)
        {
            return new AdaptationRecordResult
            {
                Outcome = DiagnosticOutcome.NoChange,
                ScheduleTask = task
            };
        }

        var prereqUt = await _db.UserTopics.FirstOrDefaultAsync(
            ut => ut.UserId == userId && ut.TopicId == prereqTopicId,
            ct);

        if (prereqUt is null)
        {
            _log.LogWarning(
                "RecordDiagnostic: no UserTopic for user {UserId} prerequisite topic {PrereqId}.",
                userId, prereqTopicId);
            return null;
        }

        UserTopic? mainUt = null;
        if (task.MainTopicId is { } mainTopicId)
            mainUt = await _db.UserTopics.FirstOrDefaultAsync(
                ut => ut.UserId == userId && ut.TopicId == mainTopicId,
                ct);
        else
            _log.LogWarning("RecordDiagnostic: task {TaskId} missing MainTopicId; main topic not updated.", taskId);

        var now = DateTime.UtcNow;
        DiagnosticOutcome outcome;

        if (result == DiagnosticResult.Passed)
        {
            prereqUt.MasteryStatus = MasteryStatus.Mastered;
            prereqUt.MasteryConfidence = Math.Max(prereqUt.MasteryConfidence, PassedConfidenceFloor);
            prereqUt.LastEvaluatedAt = now;

            if (mainUt is not null)
            {
                mainUt.IsLocked = false;
                mainUt.LastEvaluatedAt = now;
            }

            task.Status = ScheduleTaskStatus.Completed;
            outcome = DiagnosticOutcome.Cleared;
        }
        else
        {
            prereqUt.MasteryStatus = MasteryStatus.NeedsReview;
            prereqUt.MasteryConfidence = 0.0;
            prereqUt.LastEvaluatedAt = now;

            if (mainUt is not null)
            {
                mainUt.IsLocked = true;
                mainUt.LastEvaluatedAt = now;
                outcome = DiagnosticOutcome.TopicLocked;
            }
            else
                outcome = DiagnosticOutcome.PrerequisiteDowngraded;

            task.Status = result == DiagnosticResult.Failed
                ? ScheduleTaskStatus.Completed
                : ScheduleTaskStatus.Skipped;
        }

        task.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var reloaded = await _db.ScheduleTasks
            .AsNoTracking()
            .Include(t => t.Topic)
            .FirstAsync(t => t.Id == taskId, ct);

        return new AdaptationRecordResult { Outcome = outcome, ScheduleTask = reloaded };
    }

    public async Task<TopicProgressDto?> GetTopicProgressAsync(int userId, int topicId, CancellationToken ct = default)
    {
        var ut = await _db.UserTopics.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.TopicId == topicId, ct);

        if (ut is null)
            return null;

        string? lockReason = ut.IsLocked ? "Ön koşul konusu tekrar edilmeli" : null;

        return new TopicProgressDto
        {
            TopicId = topicId,
            MasteryStatus = ut.MasteryStatus,
            MasteryConfidence = ut.MasteryConfidence,
            IsLocked = ut.IsLocked,
            LockReason = lockReason
        };
    }

    /// <summary>Dışarıda öğrenildi işareti: <see cref="MasteryStatus.LearnedExternally"/> + güven 0.55.</summary>
    public static void ApplyLearnedExternally(UserTopic ut)
    {
        ut.MasteryStatus = MasteryStatus.LearnedExternally;
        ut.MasteryConfidence = LearnedExternallyConfidence;
    }

    private async Task<bool> ShouldThrottleDiagnosticAsync(int userId, int prereqTopicId, int topicId, CancellationToken ct)
    {
        var recent = await _db.ScheduleTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.TaskType == TaskType.DiagnosticTest
                        && t.PrerequisiteTopicId == prereqTopicId
                        && t.MainTopicId == topicId)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(DiagnosticSkipGuardWindow)
            .Select(t => t.Status)
            .ToListAsync(ct);

        return recent.Count >= DiagnosticSkipGuardWindow
               && recent.All(s => s == ScheduleTaskStatus.Skipped);
    }
}
