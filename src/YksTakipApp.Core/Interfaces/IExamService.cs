using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface IExamService
    {
        Task AddExamAsync(int userId, string name, DateTime date, double netTyt, double netAyt);
        Task<IEnumerable<ExamResult>> GetUserExamsAsync(int userId);
        Task DeleteExamAsync(int userId, int examId);
    }
}
