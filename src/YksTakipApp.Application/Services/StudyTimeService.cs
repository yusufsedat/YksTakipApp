using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
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
            => await CreateOrAccumulateStudyTimeAsync(userId, minutes, date, topicId);

        public async Task<StudyTime> CreateOrAccumulateStudyTimeAsync(int userId, int minutes, DateTime date, int? topicId)
            => (await CreateOrAccumulateStudyTimeIdempotentAsync(userId, minutes, date, topicId, clientRequestId: null)).Entity;

        public async Task<IdempotentCreateResult<StudyTime>> CreateOrAccumulateStudyTimeIdempotentAsync(
            int userId,
            int minutes,
            DateTime date,
            int? topicId,
            string? clientRequestId)
        {
            if (!string.IsNullOrWhiteSpace(clientRequestId))
            {
                var existingByRequest = await _db.StudyTimes
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.ClientRequestId == clientRequestId);
                if (existingByRequest is not null)
                    return new IdempotentCreateResult<StudyTime>(existingByRequest, IsReplay: true);
            }

            var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            var day = utcDate.Date;

            if (topicId.HasValue)
            {
                var inList = await _db.UserTopics.AnyAsync(ut => ut.UserId == userId && ut.TopicId == topicId.Value);
                if (!inList)
                    throw new InvalidOperationException("Seçilen konu çalışma listenizde yok. Önce Konular’dan ekleyin.");
            }

            var existing = await _db.StudyTimes.FirstOrDefaultAsync(s =>
                s.UserId == userId
                && s.TopicId == topicId
                && s.Date >= day
                && s.Date < day.AddDays(1));

            if (existing is not null)
            {
                existing.DurationMinutes += minutes;
                if (!string.IsNullOrWhiteSpace(clientRequestId))
                    existing.ClientRequestId = clientRequestId;
                await _db.SaveChangesAsync();
                return new IdempotentCreateResult<StudyTime>(existing, IsReplay: false);
            }

            var studyTime = new StudyTime
            {
                UserId = userId,
                ClientRequestId = clientRequestId,
                DurationMinutes = minutes,
                Date = utcDate,
                TopicId = topicId
            };

            _db.StudyTimes.Add(studyTime);
            try
            {
                await _db.SaveChangesAsync();
                return new IdempotentCreateResult<StudyTime>(studyTime, IsReplay: false);
            }
            catch (DbUpdateException)
            {
                if (!string.IsNullOrWhiteSpace(clientRequestId))
                {
                    var replay = await _db.StudyTimes
                        .FirstOrDefaultAsync(s => s.UserId == userId && s.ClientRequestId == clientRequestId);
                    if (replay is not null)
                        return new IdempotentCreateResult<StudyTime>(replay, IsReplay: true);
                }

                throw;
            }
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
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Date >= weekAgo)
                .ToListAsync();
            return times.Sum(s => s.DurationMinutes);
        }
    }
}
