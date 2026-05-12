using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

/// <summary>
/// Durumsuz öneri motoru: yalnızca okuma (AsNoTracking), skor hesaplar ve liste döner.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RecommendationService> _log;

    public RecommendationService(AppDbContext db, ILogger<RecommendationService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<List<TopicPriorityDto>> GetDailyRecommendationsAsync(int userId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var fatigueSince = today.AddDays(-2);
        var since30 = today.AddDays(-30);

        var topics = await _db.Topics
            .AsNoTracking()
            .ToListAsync(ct);

        // Plan: son 30 günlük StudyTime (performans); neglect/fatigue bu pencereden türetilir.
        var studyRows = await _db.StudyTimes
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.TopicId != null && s.Date >= since30)
            .Select(s => new StudyRow(s.TopicId!.Value, s.Date))
            .ToListAsync(ct);

        var exams = await _db.ExamResults
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Date)
            .Take(5)
            .Include(e => e.ExamDetails)
            .ToListAsync(ct);

        var lastStudyByTopic = new Dictionary<int, DateTime>();
        var fatigueTopicIds = new HashSet<int>();
        foreach (var row in studyRows)
        {
            if (lastStudyByTopic.TryGetValue(row.TopicId, out var existing))
            {
                if (row.Date > existing)
                    lastStudyByTopic[row.TopicId] = row.Date;
            }
            else
            {
                lastStudyByTopic[row.TopicId] = row.Date;
            }

            if (row.Date >= fatigueSince)
                fatigueTopicIds.Add(row.TopicId);
        }

        var userTopics = await _db.UserTopics.AsNoTracking()
            .Where(ut => ut.UserId == userId)
            .ToDictionaryAsync(ut => ut.TopicId, ct);
        var results = new List<(Topic Topic, int Score, string Reason, RecommendationType Type, RecommendationReasonCode ReasonCode, string ReasonShort, IReadOnlyDictionary<string, string> ReasonMeta)>(topics.Count);

        foreach (var topic in topics)
        {
            const int baseScore = 10;

            int neglectPts;
            int daysSince = 0;
            if (!lastStudyByTopic.TryGetValue(topic.Id, out var lastStudy))
            {
                neglectPts = 30;
            }
            else
            {
                daysSince = (today - lastStudy.Date).Days;
                if (daysSince < 0)
                    daysSince = 0;
                neglectPts = Math.Min(40, daysSince * 2);
            }

            var hasTopicScopedExam = exams.Any(e => e.TopicId == topic.Id);
            int wrongBlankCount;
            int weaknessPts;
            if (hasTopicScopedExam)
            {
                wrongBlankCount = exams
                    .Where(e => e.TopicId == topic.Id)
                    .Sum(e => e.ExamDetails.Sum(d => d.Wrong + d.Blank));
                weaknessPts = Math.Min(30, 5 * wrongBlankCount);
            }
            else
            {
                wrongBlankCount = exams
                    .Sum(e => e.ExamDetails
                        .Where(d => SubjectEquals(d.Subject, topic.Subject))
                        .Sum(d => d.Wrong + d.Blank));
                weaknessPts = Math.Min(15, 3 * wrongBlankCount);
            }

            var fatigueApplied = fatigueTopicIds.Contains(topic.Id);

            var raw = baseScore + neglectPts + weaknessPts - (fatigueApplied ? 25 : 0);
            var mult = ClampOsym(topic.OsymWeight);
            var finalScore = (int)Math.Round(raw * mult, MidpointRounding.AwayFromZero);

            var reason = BuildReason(
                topic,
                neglectPts,
                daysSince,
                lastStudyByTopic.ContainsKey(topic.Id),
                weaknessPts,
                wrongBlankCount,
                hasTopicScopedExam,
                fatigueApplied);

            var reasonPack = BuildReasonPack(topic, weaknessPts, neglectPts, userTopics.TryGetValue(topic.Id, out var ut) ? ut : null);
            var recType = PickRecommendationType(neglectPts, weaknessPts, fatigueApplied);
            _log.LogInformation("Recommendation reason selected. UserId={UserId} TopicId={TopicId} ReasonCode={ReasonCode}", userId, topic.Id, reasonPack.Code);
            results.Add((topic, finalScore, reason, recType, reasonPack.Code, reasonPack.Short, reasonPack.Meta));
        }

        return results
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => new TopicPriorityDto(
                x.Topic.Id,
                x.Topic.Name,
                x.Topic.Subject,
                x.Score,
                x.Reason,
                x.Type,
                x.ReasonCode,
                x.ReasonShort,
                x.ReasonMeta))
            .ToList();
    }

    private static bool SubjectEquals(string detailSubject, string topicSubject) =>
        string.Equals(detailSubject.Trim(), topicSubject.Trim(), StringComparison.OrdinalIgnoreCase);

    private static double ClampOsym(double w)
    {
        if (w < 1.0) return 1.0;
        if (w > 1.5) return 1.5;
        return w;
    }

    private static RecommendationType PickRecommendationType(int neglectPts, int weaknessPts, bool fatigueApplied)
    {
        if (weaknessPts > neglectPts && weaknessPts > 0)
            return RecommendationType.Practice;
        if (fatigueApplied)
            return RecommendationType.Review;
        return RecommendationType.TopicStudy;
    }

    private static string BuildReason(
        Topic topic,
        int neglectPts,
        int daysSince,
        bool hasAnyStudy,
        int weaknessPts,
        int wrongBlankCount,
        bool usedTopicScopedWeakness,
        bool fatigueApplied)
    {
        var fatigueMag = fatigueApplied ? 25 : 0;
        var dominant = Math.Max(neglectPts, Math.Max(weaknessPts, fatigueMag));

        if (dominant == weaknessPts && weaknessPts > 0)
        {
            var scope = usedTopicScopedWeakness
                ? "branş denemelerinde bu konu için"
                : $"\"{topic.Subject}\" dersi kapsamında son denemelerde";
            return $"{scope} toplam {wrongBlankCount} yanlış veya boş soru görüldü; pratik önerilir.";
        }

        if (dominant == fatigueMag && fatigueApplied)
            return "Bu konu son günlerde çalışıldı; kısa tekrar / hafif gözden geçirme daha uygun.";

        if (!hasAnyStudy)
            return "Henüz bu konuda çalışma kaydı yok; öncelikli çalışma adayı.";

        if (daysSince <= 0)
            return "Bugün çalışılmış; tekrar sıklığını dengelemek için düşük öncelik.";

        return $"Son {daysSince} gündür çalışılmıyor; tekrar zamanı gelmiş olabilir.";
    }

    private static (RecommendationReasonCode Code, string Short, IReadOnlyDictionary<string, string> Meta) BuildReasonPack(
        Topic topic,
        int weaknessPts,
        int neglectPts,
        UserTopic? userTopic)
    {
        if (weaknessPts >= 10)
            return (RecommendationReasonCode.WeakExamTrend, "Bu konuyu deneme sonuclarinda zayif kaldigin icin one aldim.", new Dictionary<string, string>());
        if (userTopic?.IsLocked == true || userTopic?.MasteryStatus == Core.Enums.MasteryStatus.NeedsReview)
            return (RecommendationReasonCode.MasteryRisk, "Bu konuda mastery riski goruldugu icin plana ekledim.", new Dictionary<string, string>());
        if (topic.OsymWeight >= 1.3)
            return (RecommendationReasonCode.HighOsymWeight, "Bu konu hedef bolumun icin yuksek agirlikli oldugu icin plana eklendi.", new Dictionary<string, string> { ["osymWeight"] = topic.OsymWeight.ToString("0.00") });
        if (neglectPts >= 10)
            return (RecommendationReasonCode.LowStudyTime, "Son gunlerde bu konuya calisma suren dusuk kaldi.", new Dictionary<string, string>());
        return (RecommendationReasonCode.LowStudyTime, "Calisma dengeni korumak icin bu konuyu one aldim.", new Dictionary<string, string>());
    }

    private readonly record struct StudyRow(int TopicId, DateTime Date);
}
