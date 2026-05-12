using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces
{
    public interface IStudyTimeService
    {
        Task AddStudyTimeAsync(int userId, int minutes, DateTime date, int? topicId);
        Task<StudyTime> CreateOrAccumulateStudyTimeAsync(int userId, int minutes, DateTime date, int? topicId);
        Task<IdempotentCreateResult<StudyTime>> CreateOrAccumulateStudyTimeIdempotentAsync(int userId, int minutes, DateTime date, int? topicId, string? clientRequestId);
        Task<IEnumerable<StudyTime>> GetStudyTimesAsync(int userId);
        Task<int> GetTotalMinutesLast7DaysAsync(int userId);
    }
}
