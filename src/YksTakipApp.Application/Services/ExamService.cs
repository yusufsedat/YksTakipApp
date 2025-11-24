using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class ExamService : IExamService
    {
        private readonly IRepository<ExamResult> _examRepo;

        public ExamService(IRepository<ExamResult> examRepo)
        {
            _examRepo = examRepo;
        }

        public async Task AddExamAsync(int userId, string name, DateTime date, double netTyt, double netAyt)
        {
            await _examRepo.AddAsync(new ExamResult
            {
                UserId = userId,
                ExamName = name,
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                NetTyt = netTyt,
                NetAyt = netAyt
            });
            await _examRepo.SaveChangesAsync();
        }

        public async Task<IEnumerable<ExamResult>> GetUserExamsAsync(int userId)
            => await _examRepo.FindAsync(e => e.UserId == userId);

        public async Task DeleteExamAsync(int userId, int examId)
        {
            var exam = (await _examRepo.FindAsync(e => e.Id == examId && e.UserId == userId)).FirstOrDefault();
            if (exam != null)
            {
                _examRepo.Remove(exam);
                await _examRepo.SaveChangesAsync();
            }
        }
    }
}
