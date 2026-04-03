using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface IScheduleService
    {
        Task<IReadOnlyList<ScheduleEntry>> GetListAsync(int userId);
        Task<ScheduleEntry> AddAsync(int userId, string recurrence, int? dayOfWeek, int? dayOfMonth, int startMinute, int endMinute, string title, int? topicId);
        Task UpdateAsync(int userId, int id, string recurrence, int? dayOfWeek, int? dayOfMonth, int startMinute, int endMinute, string title, int? topicId);
        Task DeleteAsync(int userId, int id);
    }
}
