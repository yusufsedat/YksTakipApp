using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class ExamService : IExamService
    {
        private readonly DbContext _db;

        public ExamService(DbContext db)
        {
            _db = db;
        }

        public async Task AddExamAsync(int userId, string name, DateTime date, double netTyt, double netAyt,
            string examType, string? subject, int? durationMinutes, int? difficulty,
            string? errorReasons, IEnumerable<ExamDetail>? details)
        {
            var exam = new ExamResult
            {
                UserId = userId,
                ExamName = name,
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                NetTyt = netTyt,
                NetAyt = netAyt,
                ExamType = examType,
                Subject = subject,
                DurationMinutes = durationMinutes,
                Difficulty = difficulty,
                ErrorReasons = errorReasons
            };

            if (details != null)
            {
                foreach (var d in details)
                {
                    d.Net = d.Correct - d.Wrong / 4.0;
                    exam.ExamDetails.Add(d);
                }
            }

            _db.Set<ExamResult>().Add(exam);
            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<ExamResult>> GetUserExamsAsync(int userId, string? type = null)
        {
            var query = _db.Set<ExamResult>()
                .Include(e => e.ExamDetails)
                .AsNoTracking()
                .Where(e => e.UserId == userId);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(e => e.ExamType == type);

            return await query.OrderByDescending(e => e.Date).ToListAsync();
        }

        public async Task DeleteExamAsync(int userId, int examId)
        {
            var exam = await _db.Set<ExamResult>()
                .FirstOrDefaultAsync(e => e.Id == examId && e.UserId == userId);

            if (exam != null)
            {
                _db.Set<ExamResult>().Remove(exam);
                await _db.SaveChangesAsync();
            }
        }
    }
}
