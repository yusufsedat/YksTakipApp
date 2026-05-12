using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class DynamicPlannerService : IDynamicPlannerService
{
    private const int MED = 30;
    private const int ReviewTaskDuration = 25;
    private const int MaxWeeklyInjectedReviewTasks = 4;
    private const double MinBufferRate = 0.10;
    private const double MaxBufferRate = 0.35;

    private readonly AppDbContext _db;
    private readonly IRecommendationService _recommendations;
    private readonly IAdaptationService _adaptation;
    private readonly IPlannerDecisionContextBuilder _decisionBuilder;
    private readonly IPlannerDecisionLogger _decisionLogger;
    private readonly IPlanQualityScorer _qualityScorer;
    private readonly ILogger<DynamicPlannerService> _log;

    public DynamicPlannerService(
        AppDbContext db,
        IRecommendationService recommendations,
        IAdaptationService adaptation,
        IPlannerDecisionContextBuilder decisionBuilder,
        IPlannerDecisionLogger decisionLogger,
        IPlanQualityScorer qualityScorer,
        ILogger<DynamicPlannerService> log)
    {
        _db = db;
        _recommendations = recommendations;
        _adaptation = adaptation;
        _decisionBuilder = decisionBuilder;
        _decisionLogger = decisionLogger;
        _qualityScorer = qualityScorer;
        _log = log;
    }

    public async Task<PlanGenerationResult> GenerateWeeklyPlanAsync(int userId, DateOnly startDate, PlannerCallMetadata? metadata = null, CancellationToken ct = default)
    {
        var operationName = "Planner.GenerateIncremental";
        var sw = Stopwatch.StartNew();
        var weekEnd = startDate.AddDays(6);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reviseFrom = today > startDate ? today : startDate;

        var goal = await ResolveActiveGoalAsync(userId, ct);
        if (goal is null)
        {
            _log.LogInformation("User {UserId}: no active goal; plan generation requires goal.", userId);
            await LogDecisionAsync(new PlannerDecisionInput
            {
                UserId = userId,
                WeekStart = startDate,
                WeekEnd = weekEnd,
                Status = PlanGenerationStatus.NoPlanGenerated,
                ReasonCode = PlanGenerationReasonCode.RequiresGoal,
                CorrelationId = metadata?.CorrelationId,
                IdempotencyKey = metadata?.IdempotencyKey,
                DurationMs = (int)sw.ElapsedMilliseconds
            }, ct);
            return PlanGenerationResult.RequiresGoal();
        }

        await ExpirePriorityRequestsAsync(userId, ct);

        var dailyCap = goal.DailyAvailableMinutes;
        var dynamicBuffer = await CalculateDynamicBufferRateAsync(userId, startDate, weekEnd, ct);
        var effectiveCapacityMultiplier = await CalculateCapacityMultiplierAsync(userId, ct);
        var effectiveDailyCap = (int)Math.Round(dailyCap * effectiveCapacityMultiplier, MidpointRounding.AwayFromZero);
        var workingDaily = (int)Math.Floor(Math.Max(MED, effectiveDailyCap) * (1 - dynamicBuffer));
        var bufferDaily = Math.Max(0, dailyCap - workingDaily);
        var priorityDuration = Math.Min(MED, workingDaily);

        if (workingDaily < MED)
        {
            _log.LogWarning(
                "User {UserId}: working daily capacity {WorkingDaily} below MED {Med}; skipping plan generation.",
                userId, workingDaily, MED);
            await LogDecisionAsync(new PlannerDecisionInput
            {
                UserId = userId,
                WeekStart = startDate,
                WeekEnd = weekEnd,
                Status = PlanGenerationStatus.NoPlanGenerated,
                ReasonCode = PlanGenerationReasonCode.DailyCapacityTooLow,
                DailyCapacity = dailyCap,
                WorkingDaily = workingDaily,
                BufferDaily = bufferDaily,
                EffectiveCapacityMultiplier = effectiveCapacityMultiplier,
                DynamicBufferRate = dynamicBuffer,
                CorrelationId = metadata?.CorrelationId,
                IdempotencyKey = metadata?.IdempotencyKey,
                DurationMs = (int)sw.ElapsedMilliseconds
            }, ct);
            return PlanGenerationResult.DailyCapacityTooLow(workingDaily, MED);
        }

        var totalDays = weekEnd.DayNumber - startDate.DayNumber + 1;
        var remaining = Enumerable.Repeat(workingDaily, totalDays).ToArray();
        var now = DateTime.UtcNow;
        var newTasks = new List<ScheduleTask>();
        var placedPriorityTopicIds = new HashSet<int>();
        var priorityTopicIds = await _db.UserTopics
            .AsNoTracking()
            .Where(ut => ut.UserId == userId
                         && ut.IsPriorityRequested
                         && ut.PriorityResolvedAt == null
                         && (ut.PriorityExpiresAt == null || ut.PriorityExpiresAt > now))
            .Select(ut => ut.TopicId)
            .Distinct()
            .ToListAsync(ct);
        var preferredDayOrder = BuildPriorityDayOrder(startDate, weekEnd, reviseFrom);

        var preservedTasks = await _db.ScheduleTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.TaskDate >= startDate
                        && t.TaskDate <= weekEnd
                        && (t.TaskDate < reviseFrom || t.Status == ScheduleTaskStatus.Completed || t.TaskType == TaskType.DiagnosticTest))
            .ToListAsync(ct);

        foreach (var task in preservedTasks)
        {
            var day = task.TaskDate.DayNumber - startDate.DayNumber;
            if (day >= 0 && day < remaining.Length)
                remaining[day] = Math.Max(0, remaining[day] - task.DurationMinutes);
        }

        var alreadyScheduledTopicIds = preservedTasks
            .Where(t => t.TaskType is TaskType.Study or TaskType.Review)
            .Select(t => t.TopicId)
            .ToHashSet();

        var prioritySkippedTopicIds = new List<int>();
        foreach (var topicId in priorityTopicIds)
        {
            if (alreadyScheduledTopicIds.Contains(topicId))
            {
                prioritySkippedTopicIds.Add(topicId);
                continue;
            }

            var placed = false;
            foreach (var day in preferredDayOrder)
            {
                var dayDate = startDate.AddDays(day);
                if (dayDate < reviseFrom)
                    continue;
                if (remaining[day] < priorityDuration)
                    continue;

                remaining[day] -= priorityDuration;
                newTasks.Add(new ScheduleTask
                {
                    UserId = userId,
                    TopicId = topicId,
                    TaskDate = startDate.AddDays(day),
                    DurationMinutes = priorityDuration,
                    Status = ScheduleTaskStatus.Planned,
                    IsRecoveryTask = false,
                    TaskType = YksTakipApp.Core.Enums.TaskType.Study,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                placedPriorityTopicIds.Add(topicId);
                alreadyScheduledTopicIds.Add(topicId);
                placed = true;
                break;
            }

            if (!placed)
                prioritySkippedTopicIds.Add(topicId);
        }

        var recs = await _recommendations.GetDailyRecommendationsAsync(userId, ct);
        var weightedRecs = recs
            .Where(r => !placedPriorityTopicIds.Contains(r.TopicId))
            .ToList();

        var recCandidateCount = weightedRecs.Count;
        var topRecPriorityScore = weightedRecs.Count == 0 ? 0 : weightedRecs.Max(r => r.PriorityScore);
        var recScheduledCount = 0;
        var skippedByCapacity = 0;
        var skippedByDuplicate = 0;

        if (weightedRecs.Count > 0)
        {
            var workingWeekly = remaining.Sum();
            var totalScore = 0;
            foreach (var r in weightedRecs)
                totalScore += Math.Max(1, r.PriorityScore);

            var jobs = new List<(int TopicId, int Duration)>(weightedRecs.Count);
            foreach (var r in weightedRecs)
            {
                if (alreadyScheduledTopicIds.Contains(r.TopicId))
                {
                    skippedByDuplicate++;
                    continue;
                }
                var score = Math.Max(1, r.PriorityScore);
                var raw = totalScore == 0 ? MED : workingWeekly * (double)score / totalScore;
                var dur = Math.Max(MED, RoundTo5(raw));
                dur = Math.Min(dur, workingDaily);
                jobs.Add((r.TopicId, dur));
            }

            jobs.Sort((a, b) => b.Duration.CompareTo(a.Duration));

            foreach (var (topicId, dur) in jobs)
            {
                var candidates = Enumerable.Range(0, 7).Where(d => remaining[d] >= dur).ToList();
                if (candidates.Count == 0)
                {
                    skippedByCapacity++;
                    continue;
                }

                var bestDay = candidates.Aggregate((a, b) => remaining[a] >= remaining[b] ? a : b);
                if (startDate.AddDays(bestDay) < reviseFrom)
                {
                    skippedByCapacity++;
                    continue;
                }
                remaining[bestDay] -= dur;

                newTasks.Add(new ScheduleTask
                {
                    UserId = userId,
                    TopicId = topicId,
                    TaskDate = startDate.AddDays(bestDay),
                    DurationMinutes = dur,
                    Status = ScheduleTaskStatus.Planned,
                    IsRecoveryTask = false,
                    TaskType = YksTakipApp.Core.Enums.TaskType.Study,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                alreadyScheduledTopicIds.Add(topicId);
                recScheduledCount++;
            }
        }

        var injectedReviewTasks = await InjectReviewTasksAsync(
            userId,
            startDate,
            weekEnd,
            reviseFrom,
            remaining,
            alreadyScheduledTopicIds,
            newTasks,
            now,
            ct);

        if (newTasks.Count == 0 && preservedTasks.Count == 0)
        {
            // Konu yok -> NoTopics, konu var ama öneri/priority üretilemedi -> NoRecommendations.
            var hasAnyUserTopic = await _db.UserTopics
                .AsNoTracking()
                .AnyAsync(ut => ut.UserId == userId, ct);
            var noPlanReason = hasAnyUserTopic ? PlanGenerationReasonCode.NoRecommendations : PlanGenerationReasonCode.NoTopics;
            if (!hasAnyUserTopic)
                _log.LogInformation("User {UserId}: no user topics; skipping plan generation.", userId);
            else
                _log.LogInformation("User {UserId}: topics exist but no recommendations/priority/preserved tasks; skipping plan generation.", userId);

            await LogDecisionAsync(new PlannerDecisionInput
            {
                UserId = userId,
                WeekStart = startDate,
                WeekEnd = weekEnd,
                Status = PlanGenerationStatus.NoPlanGenerated,
                ReasonCode = noPlanReason,
                DailyCapacity = dailyCap,
                WorkingDaily = workingDaily,
                BufferDaily = bufferDaily,
                EffectiveCapacityMultiplier = effectiveCapacityMultiplier,
                DynamicBufferRate = dynamicBuffer,
                PriorityActiveCount = priorityTopicIds.Count,
                PriorityPlacedCount = placedPriorityTopicIds.Count,
                PrioritySkippedTopicIds = prioritySkippedTopicIds,
                RecommendationCandidateCount = recCandidateCount,
                RecommendationScheduledCount = recScheduledCount,
                RecommendationSkippedByCapacityCount = skippedByCapacity,
                RecommendationSkippedByDuplicateCount = skippedByDuplicate,
                TopRecommendationPriorityScore = topRecPriorityScore,
                InjectedReviewTaskCount = injectedReviewTasks,
                PerDayRemaining = remaining,
                CorrelationId = metadata?.CorrelationId,
                IdempotencyKey = metadata?.IdempotencyKey,
                DurationMs = (int)sw.ElapsedMilliseconds
            }, ct);

            return hasAnyUserTopic ? PlanGenerationResult.NoRecommendations() : PlanGenerationResult.NoTopics();
        }

        if (skippedByCapacity > 0)
            _log.LogInformation("User {UserId}: LPT skipped {Skipped} task(s) (insufficient daily slots).", userId, skippedByCapacity);

        _log.LogInformation(
            "{OperationName} UserId={UserId} WeekStart={WeekStart} WeekEnd={WeekEnd} BufferRate={BufferRate} PriorityActiveCount={PriorityActiveCount} InjectedReviewTaskCount={InjectedReviewTaskCount} IncrementalReviseRange={IncrementalReviseRange} EffectiveCapacityMultiplier={EffectiveCapacityMultiplier} DailyCapacity={DailyCap} WorkingDaily={WorkingDaily} BufferDaily={BufferDaily}",
            operationName, userId, startDate, weekEnd, dynamicBuffer, priorityTopicIds.Count, injectedReviewTasks, $"{reviseFrom}..{weekEnd}", effectiveCapacityMultiplier, dailyCap, workingDaily, bufferDaily);

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var canTransact = _db.Database.IsRelational();
            await using var tx = canTransact ? await _db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                var toDeleteQuery = _db.ScheduleTasks
                    .Where(t =>
                        t.UserId == userId
                        && t.TaskDate >= reviseFrom
                        && t.TaskDate <= weekEnd
                        && t.Status != ScheduleTaskStatus.Completed
                        && t.TaskType != TaskType.DiagnosticTest);

                if (_db.Database.IsRelational())
                {
                    await toDeleteQuery.ExecuteDeleteAsync(ct);
                }
                else
                {
                    var toDelete = await toDeleteQuery.ToListAsync(ct);
                    _db.ScheduleTasks.RemoveRange(toDelete);
                }

                if (newTasks.Count > 0)
                    await _db.ScheduleTasks.AddRangeAsync(newTasks, ct);

                await _db.SaveChangesAsync(ct);
                if (tx is not null)
                    await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx is not null)
                    await tx.RollbackAsync(ct);
                throw;
            }
        });

        _log.LogInformation(
            "User {UserId}: generated {Count} incremental schedule task(s) for week starting {Start}. PriorityPlaced={PriorityPlaced}.",
            userId, newTasks.Count, startDate, placedPriorityTopicIds.Count);

        var saved = await GetWeeklyTasksAsync(userId, startDate, weekEnd, ct);

        var taskCountStudy = saved.Count(t => t.TaskType == TaskType.Study);
        var taskCountReview = saved.Count(t => t.TaskType == TaskType.Review);
        var taskCountDiagnostic = saved.Count(t => t.TaskType == TaskType.DiagnosticTest);

        var scoringInput = new PlanScoringInput
        {
            WorkingDaily = workingDaily,
            PerDayRemaining = remaining,
            Tasks = saved.Select(t => new ScheduledTaskSnapshot(
                t.TopicId,
                t.Topic?.Subject ?? string.Empty,
                t.TaskDate,
                t.DurationMinutes)).ToList(),
            PriorityActiveCount = priorityTopicIds.Count,
            PriorityPlacedCount = placedPriorityTopicIds.Count,
            RecommendationCandidateCount = recCandidateCount,
            RecommendationScheduledCount = recScheduledCount
        };
        var qualityScore = _qualityScorer.Score(scoringInput);
        if (qualityScore.Band != PlanQualityBand.Healthy)
        {
            _log.LogWarning(
                "User {UserId}: plan quality {Band} score={Total} (capacity={CapacityFit} priority={PriorityCoverage} weakness={WeaknessCoverage} subject={SubjectBalance} repetition={RepetitionSafety} overload={OverloadSafety}).",
                userId,
                qualityScore.Band,
                qualityScore.Total,
                qualityScore.CapacityFit,
                qualityScore.PriorityCoverage,
                qualityScore.WeaknessCoverage,
                qualityScore.SubjectBalance,
                qualityScore.RepetitionSafety,
                qualityScore.OverloadSafety);
        }

        await LogDecisionAsync(new PlannerDecisionInput
        {
            UserId = userId,
            WeekStart = startDate,
            WeekEnd = weekEnd,
            Status = PlanGenerationStatus.Success,
            ReasonCode = PlanGenerationReasonCode.None,
            TaskCountTotal = saved.Count,
            TaskCountStudy = taskCountStudy,
            TaskCountReview = taskCountReview,
            TaskCountDiagnostic = taskCountDiagnostic,
            PreservedTaskCount = preservedTasks.Count,
            RecommendationCandidateCount = recCandidateCount,
            RecommendationScheduledCount = recScheduledCount,
            RecommendationSkippedByCapacityCount = skippedByCapacity,
            RecommendationSkippedByDuplicateCount = skippedByDuplicate,
            TopRecommendationPriorityScore = topRecPriorityScore,
            DailyCapacity = dailyCap,
            WorkingDaily = workingDaily,
            BufferDaily = bufferDaily,
            EffectiveCapacityMultiplier = effectiveCapacityMultiplier,
            DynamicBufferRate = dynamicBuffer,
            PriorityActiveCount = priorityTopicIds.Count,
            PriorityPlacedCount = placedPriorityTopicIds.Count,
            PrioritySkippedTopicIds = prioritySkippedTopicIds,
            InjectedReviewTaskCount = injectedReviewTasks,
            PerDayRemaining = remaining,
            QualityScore = qualityScore,
            CorrelationId = metadata?.CorrelationId,
            IdempotencyKey = metadata?.IdempotencyKey,
            DurationMs = (int)sw.ElapsedMilliseconds
        }, ct);

        return PlanGenerationResult.Success(saved);
    }

    private Task LogDecisionAsync(PlannerDecisionInput input, CancellationToken ct)
    {
        var context = _decisionBuilder.Build(input);
        return _decisionLogger.LogAsync(context, ct);
    }

    public async Task CheckAndTriggerChurnAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (today < weekStart || today > weekEnd)
            return;
        using var _ = _log.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "ChurnCheck",
            ["UserId"] = userId,
            ["WeekStart"] = weekStart
        });

        var hasPlannedToday = await _db.ScheduleTasks.AsNoTracking().AnyAsync(
            t => t.UserId == userId
                 && t.TaskDate == today
                 && t.Status == ScheduleTaskStatus.Planned
                 && t.TaskType == TaskType.Study,
            ct);
        if (!hasPlannedToday)
            await TryStoreChurnEventAsync(userId, weekStart, weekEnd, today, PlannerChurnReasonCode.NoPlannedToday, ct);
        else
            return;

        var hasAnyStudyTaskInWeek = await _db.ScheduleTasks.AsNoTracking().AnyAsync(
            t => t.UserId == userId
                 && t.TaskDate >= weekStart
                 && t.TaskDate <= weekEnd
                 && t.TaskType == TaskType.Study,
            ct);
        if (hasAnyStudyTaskInWeek)
            return;
        await TryStoreChurnEventAsync(userId, weekStart, weekEnd, today, PlannerChurnReasonCode.NoStudyTaskInWeek, ct);

        _log.LogInformation(
            "User {UserId}: churn check detected empty weekly study plan, generating week starting {WeekStart}.",
            userId, weekStart);
        await GenerateWeeklyPlanAsync(userId, weekStart, metadata: null, ct);
    }

    public async Task InvalidatePlannedWeekAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct = default)
    {
        await _db.ScheduleTasks
            .Where(t =>
                t.UserId == userId
                && t.TaskDate >= weekStart
                && t.TaskDate <= weekEnd
                && t.Status == ScheduleTaskStatus.Planned
                && t.TaskType != TaskType.DiagnosticTest)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleTask>> GetWeeklyTasksAsync(int userId, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await _db.ScheduleTasks
            .AsNoTracking()
            .Include(t => t.Topic)
            .Where(t => t.UserId == userId && t.TaskDate >= start && t.TaskDate <= end)
            .OrderBy(t => t.TaskDate)
            .ThenBy(t => t.Topic.Name)
            .ToListAsync(ct);
    }

    public async Task<ScheduleTask?> UpdateStatusAsync(int userId, int taskId, ScheduleTaskStatus status, CancellationToken ct = default)
    {
        var task = await _db.ScheduleTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct);
        if (task is null)
            return null;

        if (task.TaskType == TaskType.DiagnosticTest
            && status is ScheduleTaskStatus.Completed or ScheduleTaskStatus.Skipped or ScheduleTaskStatus.Deferred)
        {
            var dr = status == ScheduleTaskStatus.Completed
                ? DiagnosticResult.Passed
                : DiagnosticResult.SkippedOrDeleted;

            var rec = await _adaptation.RecordDiagnosticTestResultAsync(userId, taskId, dr, ct);
            return rec?.ScheduleTask;
        }

        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;
        if (status == ScheduleTaskStatus.Completed)
        {
            await _db.UserTopics
                .Where(ut => ut.UserId == userId && ut.TopicId == task.TopicId && ut.IsPriorityRequested)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ut => ut.IsPriorityRequested, false)
                    .SetProperty(ut => ut.PriorityResolvedAt, DateTime.UtcNow), ct);
        }
        await _db.SaveChangesAsync(ct);

        return await _db.ScheduleTasks
            .AsNoTracking()
            .Include(t => t.Topic)
            .FirstAsync(t => t.Id == taskId, ct);
    }

    private async Task<UserGoal?> ResolveActiveGoalAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user?.ActiveGoalVersionId is not { } activeId)
            return null;
        return await _db.UserGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == activeId, ct);
    }

    private static int RoundTo5(double value) =>
        (int)(Math.Round(value / 5.0, MidpointRounding.AwayFromZero) * 5);

    private async Task<double> CalculateDynamicBufferRateAsync(int userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct)
    {
        var endDate = weekEnd;
        var startDate = endDate.AddDays(-20);
        var planned = await _db.ScheduleTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.TaskDate >= startDate
                        && t.TaskDate <= endDate
                        && t.TaskType != TaskType.DiagnosticTest
                        && t.Status != ScheduleTaskStatus.Skipped)
            .SumAsync(t => (int?)t.DurationMinutes, ct) ?? 0;

        if (planned <= 0)
            return 0.20;

        var studyStartUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var studyEndUtc = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var actual = await _db.StudyTimes
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= studyStartUtc && s.Date < studyEndUtc)
            .SumAsync(s => (int?)s.DurationMinutes, ct) ?? 0;

        var ratio = planned <= 0 ? 0.8 : Math.Clamp(actual / (double)planned, 0.4, 1.25);
        return CalculateDynamicBufferRate(ratio);
    }

    public static double CalculateDynamicBufferRate(double executionRatio)
    {
        var ratio = Math.Clamp(executionRatio, 0.4, 1.2);
        var normalized = (ratio - 0.4) / 0.8;
        var value = MaxBufferRate - normalized * (MaxBufferRate - MinBufferRate);
        return Math.Round(Math.Clamp(value, MinBufferRate, MaxBufferRate), 4);
    }

    private async Task<double> CalculateCapacityMultiplierAsync(int userId, CancellationToken ct)
    {
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-20);
        var planned = await _db.ScheduleTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.TaskDate >= startDate
                        && t.TaskDate <= endDate
                        && t.TaskType != TaskType.DiagnosticTest
                        && t.Status == ScheduleTaskStatus.Completed)
            .SumAsync(t => (int?)t.DurationMinutes, ct) ?? 0;
        if (planned <= 0)
            return 1.0;

        var studyStartUtc = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var studyEndUtc = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var actual = await _db.StudyTimes
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= studyStartUtc && s.Date < studyEndUtc)
            .SumAsync(s => (int?)s.DurationMinutes, ct) ?? 0;
        var ratio = Math.Clamp(actual / (double)planned, 0.7, 1.2);
        return CalculateCapacityMultiplier(ratio);
    }

    public static double CalculateCapacityMultiplier(double actualToPlannedRatio)
    {
        if (actualToPlannedRatio < 0.85)
            return 0.90;
        if (actualToPlannedRatio > 1.05)
            return 1.08;
        return 1.0;
    }

    private async Task<int> InjectReviewTasksAsync(
        int userId,
        DateOnly weekStart,
        DateOnly weekEnd,
        DateOnly reviseFrom,
        int[] remaining,
        ISet<int> alreadyScheduledTopicIds,
        ICollection<ScheduleTask> output,
        DateTime now,
        CancellationToken ct)
    {
        var unresolved = await _db.ProblemNotes
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted && !p.SolutionLearned)
            .OrderByDescending(p => p.CreatedAt)
            .Take(40)
            .ToListAsync(ct);
        if (unresolved.Count == 0)
            return 0;

        var userTopicMap = await _db.UserTopics
            .AsNoTracking()
            .Include(ut => ut.Topic)
            .Where(ut => ut.UserId == userId)
            .ToListAsync(ct);

        var topicById = userTopicMap
            .Where(ut => ut.Topic is not null)
            .ToDictionary(ut => ut.TopicId, ut => ut.Topic!, EqualityComparer<int>.Default);

        var reviewTopicIds = new HashSet<int>();
        foreach (var note in unresolved)
        {
            var tags = ParseTags(note.TagsJson);
            foreach (var ut in userTopicMap)
            {
                var t = ut.Topic;
                if (t is null)
                    continue;
                if (tags.Any(tag => string.Equals(tag, t.Subject, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(tag, t.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    reviewTopicIds.Add(t.Id);
                    break;
                }
            }

            if (reviewTopicIds.Count >= MaxWeeklyInjectedReviewTasks)
                break;
        }

        if (reviewTopicIds.Count == 0)
            reviewTopicIds.UnionWith(userTopicMap.Select(ut => ut.TopicId).Take(1));

        var dayOrder = BuildPriorityDayOrder(weekStart, weekEnd, reviseFrom);
        var injected = 0;
        foreach (var topicId in reviewTopicIds)
        {
            if (injected >= MaxWeeklyInjectedReviewTasks)
                break;
            if (alreadyScheduledTopicIds.Contains(topicId))
                continue;
            if (!topicById.ContainsKey(topicId))
                continue;

            foreach (var day in dayOrder)
            {
                var dayDate = weekStart.AddDays(day);
                if (dayDate < reviseFrom)
                    continue;
                if (remaining[day] < ReviewTaskDuration)
                    continue;

                remaining[day] -= ReviewTaskDuration;
                output.Add(new ScheduleTask
                {
                    UserId = userId,
                    TopicId = topicId,
                    TaskDate = dayDate,
                    DurationMinutes = ReviewTaskDuration,
                    Status = ScheduleTaskStatus.Planned,
                    IsRecoveryTask = false,
                    TaskType = TaskType.Review,
                    Reason = "Unresolved problem-note feedback injection",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                alreadyScheduledTopicIds.Add(topicId);
                injected++;
                break;
            }
        }

        return injected;
    }

    private static IReadOnlyList<string> ParseTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return Array.Empty<string>();
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(tagsJson);
            if (parsed is null)
                return Array.Empty<string>();
            return parsed.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private async Task ExpirePriorityRequestsAsync(int userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expiring = await _db.UserTopics
            .Where(ut => ut.UserId == userId
                         && ut.IsPriorityRequested
                         && ut.PriorityResolvedAt == null
                         && ut.PriorityExpiresAt != null
                         && ut.PriorityExpiresAt <= now)
            .ToListAsync(ct);
        foreach (var item in expiring)
        {
            item.IsPriorityRequested = false;
            item.PriorityResolvedAt = now;
        }

        if (expiring.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Expired {Count} priority request(s) for user {UserId}.", expiring.Count, userId);
        }
    }

    private static IReadOnlyList<int> BuildPriorityDayOrder(DateOnly weekStart, DateOnly weekEnd, DateOnly today)
    {
        var order = new List<int>(7);
        var preferredDates = new[] { today, today.AddDays(1) };

        foreach (var date in preferredDates)
        {
            if (date < weekStart || date > weekEnd)
                continue;

            var index = date.DayNumber - weekStart.DayNumber;
            if (index >= 0 && index < 7 && !order.Contains(index))
                order.Add(index);
        }

        for (var i = 0; i < 7; i++)
        {
            if (!order.Contains(i))
                order.Add(i);
        }

        return order;
    }

    private async Task TryStoreChurnEventAsync(
        int userId,
        DateOnly weekStart,
        DateOnly weekEnd,
        DateOnly triggerDate,
        PlannerChurnReasonCode reasonCode,
        CancellationToken ct)
    {
        var exists = await _db.UserPlannerChurnEvents.AsNoTracking().AnyAsync(
            e => e.UserId == userId && e.WeekStart == weekStart && e.ReasonCode == reasonCode,
            ct);
        if (exists)
            return;

        var lastCompletedDate = await _db.ScheduleTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Status == ScheduleTaskStatus.Completed && t.TaskType == TaskType.Study)
            .OrderByDescending(t => t.TaskDate)
            .Select(t => (DateOnly?)t.TaskDate)
            .FirstOrDefaultAsync(ct);
        var firstPlanCreatedAt = await _db.ScheduleTasks
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.TaskDate >= weekStart && t.TaskDate <= weekEnd)
            .OrderBy(t => t.CreatedAt)
            .Select(t => (DateTime?)t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var entity = new UserPlannerChurnEvent
        {
            UserId = userId,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            TriggerDate = triggerDate,
            ReasonCode = reasonCode,
            DaysSinceLastCompletedTask = lastCompletedDate.HasValue ? triggerDate.DayNumber - lastCompletedDate.Value.DayNumber : null,
            DaysSincePlanGenerated = firstPlanCreatedAt.HasValue ? (triggerDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - firstPlanCreatedAt.Value).Days : null,
            CreatedAt = DateTime.UtcNow
        };
        _db.UserPlannerChurnEvents.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation(
                "Churn event stored. UserId={UserId} WeekStart={WeekStart} ReasonCode={ReasonCode} DaysSincePlanGenerated={DaysSincePlanGenerated}",
                userId, weekStart, reasonCode, entity.DaysSincePlanGenerated);
        }
        catch (DbUpdateException)
        {
            _db.Entry(entity).State = EntityState.Detached;
        }
    }

}
