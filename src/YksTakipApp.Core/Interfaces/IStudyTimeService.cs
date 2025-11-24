using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface IStudyTimeService
    {
        Task AddStudyTimeAsync(int userId, int minutes, DateTime date);
        Task<IEnumerable<StudyTime>> GetStudyTimesAsync(int userId);
        Task<int> GetTotalMinutesLast7DaysAsync(int userId);
    }
}
