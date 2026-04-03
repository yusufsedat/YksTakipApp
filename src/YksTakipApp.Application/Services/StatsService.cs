using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class StatsService : IStatsService
    {
        private readonly IRepository<StudyTime> _studyRepo;
        private readonly IRepository<UserTopic> _topicRepo;
        private readonly DbContext _db;

        public StatsService(IRepository<StudyTime> studyRepo, IRepository<UserTopic> topicRepo, DbContext db)
        {
            _studyRepo = studyRepo;
            _topicRepo = topicRepo;
            _db = db;
        }

        public async Task<object> GetSummaryAsync(int userId)
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var studyTimes = await _studyRepo.FindAsync(s => s.UserId == userId && s.Date >= weekAgo);
            var topics = await _topicRepo.FindAsync(t => t.UserId == userId && t.Status == TopicStatus.Completed);
            var exams = await _db.Set<ExamResult>().AsNoTracking()
                .Where(e => e.UserId == userId).ToListAsync();

            double avgTyt = exams.Any() ? exams.Average(e => e.NetTyt) : 0;
            double avgAyt = exams.Any() ? exams.Average(e => e.NetAyt) : 0;

            return new
            {
                totalMinutesLast7Days = studyTimes.Sum(s => s.DurationMinutes),
                completedTopics = topics.Count(),
                avgTyt,
                avgAyt
            };
        }

        public async Task<object> GetWeeklyAsync(int userId)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-6);
            var endDate = DateTime.UtcNow.Date;
            var studyTimes = await _studyRepo.FindAsync(s => s.UserId == userId && s.Date >= startDate);

            var byDay = studyTimes
                .GroupBy(s => s.Date.Date)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.DurationMinutes));

            var rows = new List<object>();
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var key = d.Date;
                rows.Add(new { date = key.ToString("yyyy-MM-dd"), totalMinutes = byDay.GetValueOrDefault(key, 0) });
            }

            return rows;
        }

        public async Task<object> GetProgressAsync(int userId)
        {
            DateTime today = DateTime.UtcNow.Date;
            DateTime thisWeekStart = today.AddDays(-6);
            DateTime lastWeekStart = today.AddDays(-13);
            DateTime lastWeekEnd = thisWeekStart;

            var thisWeek = await _studyRepo.FindAsync(s => s.UserId == userId && s.Date >= thisWeekStart && s.Date <= today);
            var lastWeek = await _studyRepo.FindAsync(s => s.UserId == userId && s.Date >= lastWeekStart && s.Date < lastWeekEnd);

            int thisWeekTotal = thisWeek.Sum(s => s.DurationMinutes);
            int lastWeekTotal = lastWeek.Sum(s => s.DurationMinutes);

            double change = lastWeekTotal == 0 ? 100 : ((double)(thisWeekTotal - lastWeekTotal) / lastWeekTotal) * 100;

            return new
            {
                thisWeekMinutes = thisWeekTotal,
                lastWeekMinutes = lastWeekTotal,
                changePercent = Math.Round(change, 2)
            };
        }

        public async Task<object> GetTytStatsAsync(int userId)
        {
            var exams = await _db.Set<ExamResult>()
                .Include(e => e.ExamDetails)
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.ExamType == "TYT")
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            if (!exams.Any())
                return new { examCount = 0, avgNet = 0.0, last5 = Array.Empty<object>(), netTrend = Array.Empty<object>(), speedMetric = 0.0, subjectAverages = Array.Empty<object>() };

            var allDetails = exams.SelectMany(e => e.ExamDetails).ToList();
            double totalNet = allDetails.Any() ? allDetails.Sum(d => d.Net) : exams.Sum(e => e.NetTyt);
            double avgNet = allDetails.Any()
                ? allDetails.Sum(d => d.Net) / exams.Count
                : exams.Average(e => e.NetTyt);

            var examsWithDuration = exams.Where(e => e.DurationMinutes.HasValue && e.DurationMinutes > 0).ToList();
            double speedMetric = 0;
            if (examsWithDuration.Any())
            {
                var totalDetailNet = examsWithDuration.SelectMany(e => e.ExamDetails).Sum(d => d.Net);
                var fallbackNet = examsWithDuration.Sum(e => e.NetTyt);
                var netUsed = totalDetailNet > 0 ? totalDetailNet : fallbackNet;
                speedMetric = Math.Round(netUsed / examsWithDuration.Sum(e => e.DurationMinutes!.Value) * 60, 2);
            }

            var subjectAverages = allDetails
                .GroupBy(d => d.Subject)
                .Select(g => new { subject = g.Key, avgNet = Math.Round(g.Average(d => d.Net), 2) })
                .OrderByDescending(s => s.avgNet)
                .ToList();

            var last10Ordered = exams.Take(10).OrderBy(e => e.Date).ToList();
            var netTrend = last10Ordered.Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                totalNet = e.ExamDetails.Any() ? Math.Round(e.ExamDetails.Sum(d => d.Net), 2) : e.NetTyt
            }).ToList();

            var last5 = exams.Take(5).Select(e => new
            {
                e.Id, e.ExamName, e.Date, examType = e.ExamType,
                totalNet = e.ExamDetails.Any() ? Math.Round(e.ExamDetails.Sum(d => d.Net), 2) : e.NetTyt,
                e.DurationMinutes, e.Difficulty
            }).ToList();

            return new { examCount = exams.Count, avgNet = Math.Round(avgNet, 2), last5, netTrend, speedMetric, subjectAverages };
        }

        public async Task<object> GetAytStatsAsync(int userId)
        {
            var exams = await _db.Set<ExamResult>()
                .Include(e => e.ExamDetails)
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.ExamType == "AYT")
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            if (!exams.Any())
                return new { examCount = 0, avgNet = 0.0, last5 = Array.Empty<object>(), netTrend = Array.Empty<object>(), subjectAverages = Array.Empty<object>() };

            var allDetails = exams.SelectMany(e => e.ExamDetails).ToList();
            double avgNet = allDetails.Any()
                ? allDetails.Sum(d => d.Net) / exams.Count
                : exams.Average(e => e.NetAyt);

            var subjectAverages = allDetails
                .GroupBy(d => d.Subject)
                .Select(g => new { subject = g.Key, avgNet = Math.Round(g.Average(d => d.Net), 2) })
                .OrderByDescending(s => s.avgNet)
                .ToList();

            var last10Ordered = exams.Take(10).OrderBy(e => e.Date).ToList();
            var netTrend = last10Ordered.Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                totalNet = e.ExamDetails.Any() ? Math.Round(e.ExamDetails.Sum(d => d.Net), 2) : e.NetAyt
            }).ToList();

            var last5 = exams.Take(5).Select(e => new
            {
                e.Id, e.ExamName, e.Date, examType = e.ExamType,
                totalNet = e.ExamDetails.Any() ? Math.Round(e.ExamDetails.Sum(d => d.Net), 2) : e.NetAyt,
                e.DurationMinutes, e.Difficulty
            }).ToList();

            return new { examCount = exams.Count, avgNet = Math.Round(avgNet, 2), last5, netTrend, subjectAverages };
        }

        public async Task<object> GetBransStatsAsync(int userId)
        {
            var exams = await _db.Set<ExamResult>()
                .Include(e => e.ExamDetails)
                .AsNoTracking()
                .Where(e => e.UserId == userId && e.ExamType == "BRANS")
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            if (!exams.Any())
                return new { examCount = 0, subjects = Array.Empty<object>(), netTrend = Array.Empty<object>() };

            var subjectGroups = exams
                .GroupBy(e => e.Subject ?? "Bilinmiyor")
                .Select(g =>
                {
                    var subjectExams = g.ToList();
                    var details = subjectExams.SelectMany(e => e.ExamDetails).ToList();
                    double avgNet = details.Any() ? Math.Round(details.Average(d => d.Net), 2) : 0;
                    var difficultyDist = subjectExams
                        .Where(e => e.Difficulty.HasValue)
                        .GroupBy(e => e.Difficulty!.Value)
                        .Select(dg => new { difficulty = dg.Key, count = dg.Count() })
                        .OrderBy(x => x.difficulty)
                        .ToList();

                    var trend = subjectExams.Take(5).Select(e => new
                    {
                        e.Id, e.ExamName, e.Date,
                        totalNet = e.ExamDetails.Any() ? Math.Round(e.ExamDetails.Sum(d => d.Net), 2) : 0.0,
                        e.Difficulty
                    }).ToList();

                    return new
                    {
                        subject = g.Key,
                        examCount = subjectExams.Count,
                        avgNet,
                        difficultyDistribution = difficultyDist,
                        recentExams = trend
                    };
                })
                .OrderByDescending(s => s.examCount)
                .ToList();

            var netTrend = exams.Take(10).OrderBy(e => e.Date).Select(e => new
            {
                date = e.Date.ToString("yyyy-MM-dd"),
                totalNet = e.ExamDetails.Any() ? Math.Round(e.ExamDetails.Sum(d => d.Net), 2) : 0.0
            }).ToList();

            return new { examCount = exams.Count, subjects = subjectGroups, netTrend };
        }

        public async Task<int> GetExamStreakDaysAsync(int userId)
        {
            var times = await _db.Set<ExamResult>().AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => e.Date)
                .ToListAsync();

            var examDays = times.Select(t => t.Date).ToHashSet();
            return ComputeExamStreak(examDays);
        }

        public async Task<object> GetWinsAsync(int userId)
        {
            var streak = await GetExamStreakDaysAsync(userId);

            var rows = await (
                from ut in _db.Set<UserTopic>().AsNoTracking()
                join t in _db.Set<Topic>().AsNoTracking() on ut.TopicId equals t.Id
                where ut.UserId == userId
                select new { ut.Status, t.Category, t.Subject, t.Name }
            ).ToListAsync();

            var subjectWins = rows
                .GroupBy(x => new { x.Category, x.Subject })
                .Select(g => new
                {
                    category = g.Key.Category,
                    subject = g.Key.Subject,
                    completed = g.Count(x => x.Status == TopicStatus.Completed),
                    tracked = g.Count(),
                    completedTopicNames = g
                        .Where(x => x.Status == TopicStatus.Completed)
                        .Select(x => x.Name)
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList()
                })
                .OrderByDescending(x => x.completed)
                .ThenBy(x => x.subject)
                .ToList();

            return new { examStreakDays = streak, subjectWins };
        }

        private static int ComputeExamStreak(HashSet<DateTime> examDays)
        {
            if (examDays.Count == 0)
                return 0;

            var today = DateTime.UtcNow.Date;
            var d = today;
            if (!examDays.Contains(d))
                d = d.AddDays(-1);
            if (!examDays.Contains(d))
                return 0;

            var streak = 0;
            while (examDays.Contains(d))
            {
                streak++;
                d = d.AddDays(-1);
            }

            return streak;
        }
    }
}
