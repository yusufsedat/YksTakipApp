using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class StudyTimeService : IStudyTimeService
    {
        private readonly IRepository<StudyTime> _repository;

        public StudyTimeService(IRepository<StudyTime> repository)
        {
            _repository = repository;
        }

        public async Task AddStudyTimeAsync(int userId, int minutes, DateTime date)
        {
            var studyTime = new StudyTime
            {
                UserId = userId,
                DurationMinutes = minutes,
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc)
            };

            await _repository.AddAsync(studyTime);
            await _repository.SaveChangesAsync();
        }

        public async Task<IEnumerable<StudyTime>> GetStudyTimesAsync(int userId)
        {
            return await _repository.FindAsync(s => s.UserId == userId);
        }

        public async Task<int> GetTotalMinutesLast7DaysAsync(int userId)
        {
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var times = await _repository.FindAsync(s => s.UserId == userId && s.Date >= weekAgo);
            return times.Sum(s => s.DurationMinutes);
        }
    }
}
