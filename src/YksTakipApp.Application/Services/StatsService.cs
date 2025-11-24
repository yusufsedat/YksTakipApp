using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class StatsService : IStatsService
    {
        private readonly IRepository<StudyTime> _studyRepo;
        private readonly IRepository<UserTopic> _topicRepo;
        private readonly IRepository<ExamResult> _examRepo;

        public StatsService(IRepository<StudyTime> studyRepo, IRepository<UserTopic> topicRepo, IRepository<ExamResult> examRepo)
        {
            _studyRepo = studyRepo;
            _topicRepo = topicRepo;
            _examRepo = examRepo;
        }

        public async Task<object> GetSummaryAsync(int userId)
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var studyTimes = await _studyRepo.FindAsync(s => s.UserId == userId && s.Date >= weekAgo);
            var topics = await _topicRepo.FindAsync(t => t.UserId == userId && t.Status == TopicStatus.Completed);
            var exams = await _examRepo.FindAsync(e => e.UserId == userId);

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
            var studyTimes = await _studyRepo.FindAsync(s => s.UserId == userId && s.Date >= startDate);

            var grouped = studyTimes
                .GroupBy(s => s.Date.Date)
                .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), TotalMinutes = g.Sum(x => x.DurationMinutes) })
                .ToList();

            return grouped;
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
    }
}
