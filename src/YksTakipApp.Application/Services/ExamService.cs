using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;

namespace YksTakipApp.Application.Services
{
    public class ExamService : IExamService
    {
        private readonly DbContext _db;
        private readonly IBackgroundTaskQueue _backgroundQueue;
        private readonly ILogger<ExamService> _log;

        public ExamService(
            DbContext db,
            IBackgroundTaskQueue backgroundQueue,
            ILogger<ExamService> log)
        {
            _db = db;
            _backgroundQueue = backgroundQueue;
            _log = log;
        }

        public async Task AddExamAsync(int userId, string name, DateTime date, double netTyt, double netAyt,
            string examType, string? subject, int? durationMinutes, int? difficulty,
            string? errorReasons, IEnumerable<ExamDetail>? details)
            => await AddExamIdempotentAsync(userId, name, date, netTyt, netAyt, examType, subject, durationMinutes, difficulty, errorReasons, details, null);

        public async Task<IdempotentCreateResult<ExamResult>> AddExamIdempotentAsync(
            int userId,
            string name,
            DateTime date,
            double netTyt,
            double netAyt,
            string examType,
            string? subject,
            int? durationMinutes,
            int? difficulty,
            string? errorReasons,
            IEnumerable<ExamDetail>? details,
            string? clientRequestId)
        {
            if (!string.IsNullOrWhiteSpace(clientRequestId))
            {
                var replay = await _db.Set<ExamResult>()
                    .Include(e => e.ExamDetails)
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.ClientRequestId == clientRequestId);
                if (replay is not null)
                    return new IdempotentCreateResult<ExamResult>(replay, IsReplay: true);
            }

            var exam = new ExamResult
            {
                UserId = userId,
                ClientRequestId = clientRequestId,
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
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (!string.IsNullOrWhiteSpace(clientRequestId))
                {
                    var replay = await _db.Set<ExamResult>()
                        .Include(e => e.ExamDetails)
                        .FirstOrDefaultAsync(e => e.UserId == userId && e.ClientRequestId == clientRequestId);
                    if (replay is not null)
                    {
                        _log.LogWarning("Exam create replayed for user {UserId} and request {ClientRequestId}.", userId, clientRequestId);
                        return new IdempotentCreateResult<ExamResult>(replay, IsReplay: true);
                    }
                }

                throw;
            }

            var jobs = await BuildAdaptationJobsAsync(userId, exam, details);
            foreach (var job in jobs)
                await _backgroundQueue.EnqueueAdaptationEvaluationAsync(job);
            return new IdempotentCreateResult<ExamResult>(exam, IsReplay: false);
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

        private async Task<List<AdaptationEvaluationJob>> BuildAdaptationJobsAsync(
            int userId,
            ExamResult exam,
            IEnumerable<ExamDetail>? details)
        {
            var list = new List<AdaptationEvaluationJob>();
            var detailList = details?.ToList();
            if (detailList is null || detailList.Count == 0)
                return list;

            var adaptationSubjects = detailList
                .Where(d => d.Correct + d.Wrong + d.Blank > 0)
                .Select(d => new
                {
                    Subject = d.Subject.Trim(),
                    ScorePercent = (int)Math.Round((double)d.Correct * 100 / (d.Correct + d.Wrong + d.Blank))
                })
                .Where(x => x.ScorePercent < 20 || x.ScorePercent >= 75)
                .GroupBy(x => x.Subject, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Subject = g.Key, ScorePercent = g.Average(x => x.ScorePercent) >= 75 ? g.Max(x => x.ScorePercent) : g.Min(x => x.ScorePercent) })
                .ToList();

            if (adaptationSubjects.Count == 0)
                return list;

            var subjectSet = adaptationSubjects.Select(x => x.Subject).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = await _db.Set<UserTopic>()
                .AsNoTracking()
                .Include(ut => ut.Topic)
                .Where(ut => ut.UserId == userId
                             && ut.Topic != null
                             && subjectSet.Contains(ut.Topic.Subject)
                             && ut.Status != TopicStatus.Completed)
                .ToListAsync();

            foreach (var weak in adaptationSubjects)
            {
                var userTopic = candidates.FirstOrDefault(c =>
                    string.Equals(c.Topic.Subject.Trim(), weak.Subject, StringComparison.OrdinalIgnoreCase));
                if (userTopic is null)
                    continue;

                list.Add(new AdaptationEvaluationJob(userId, userTopic.TopicId, weak.ScorePercent));
            }

            if (list.Count > 0)
            {
                _log.LogInformation(
                    "Queued {Count} adaptation job(s) after exam {ExamId} for user {UserId}.",
                    list.Count,
                    exam.Id,
                    userId);
            }

            return list;
        }
    }
}
