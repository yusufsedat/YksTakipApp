using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services
{
    public class StudyTimeService : IStudyTimeService
    {
        private readonly AppDbContext _db;

        public StudyTimeService(AppDbContext db)
        {
            _db = db;
        }

        public async Task AddStudyTimeAsync(int userId, int minutes, DateTime date, int? topicId)
        {
            var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);

            if (topicId.HasValue)
            {
                var inList = await _db.UserTopics.AnyAsync(ut => ut.UserId == userId && ut.TopicId == topicId.Value);
                if (!inList)
                    throw new InvalidOperationException("Seçilen konu çalışma listenizde yok. Önce Konular’dan ekleyin.");
            }

            var studyTime = new StudyTime
            {
                UserId = userId,
                DurationMinutes = minutes,
                Date = utcDate,
                TopicId = topicId
            };

            _db.StudyTimes.Add(studyTime);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<StudyTime>> GetStudyTimesAsync(int userId)
        {
            return await _db.StudyTimes
                .AsNoTracking()
                .Include(s => s.Topic)
                .Where(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<int> GetTotalMinutesLast7DaysAsync(int userId)
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var times = await _db.StudyTimes
                .Where(s => s.UserId == userId && s.Date >= weekAgo)
                .ToListAsync();
            return times.Sum(s => s.DurationMinutes);
        }
    }
}
