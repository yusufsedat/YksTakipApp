using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface IExamService
    {
        Task AddExamAsync(int userId, string name, DateTime date, double netTyt, double netAyt,
            string examType, string? subject, int? durationMinutes, int? difficulty,
            string? errorReasons, IEnumerable<ExamDetail>? details);
        Task<IEnumerable<ExamResult>> GetUserExamsAsync(int userId, string? type = null);
        Task DeleteExamAsync(int userId, int examId);
    }
}
