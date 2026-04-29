using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly IRepository<ScheduleEntry> _repository;
        private readonly AppDbContext _db;

        public ScheduleService(IRepository<ScheduleEntry> repository, AppDbContext db)
        {
            _repository = repository;
            _db = db;
        }

        public async Task<IReadOnlyList<ScheduleEntry>> GetListAsync(int userId)
        {
            var items = await _db.ScheduleEntries
                .AsNoTracking()
                .Include(e => e.Topic)
                .Where(e => e.UserId == userId)
                .ToListAsync();
            items.Sort(CompareEntries);
            return items;
        }

        /// <summary>Önce haftalıklar (Pazartesi öncelikli sıra), sonra aylıklar.</summary>
        private static int CompareEntries(ScheduleEntry a, ScheduleEntry b)
        {
            var ra = string.Equals(a.Recurrence, "Monthly", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var rb = string.Equals(b.Recurrence, "Monthly", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var c = ra.CompareTo(rb);
            if (c != 0) return c;

            if (ra == 0)
            {
                var da = MondayFirst(a.DayOfWeek ?? 0);
                var db = MondayFirst(b.DayOfWeek ?? 0);
                c = da.CompareTo(db);
                if (c != 0) return c;
            }
            else
            {
                c = (a.DayOfMonth ?? 0).CompareTo(b.DayOfMonth ?? 0);
                if (c != 0) return c;
            }

            c = a.StartMinute.CompareTo(b.StartMinute);
            if (c != 0) return c;
            return a.Id.CompareTo(b.Id);
        }

        private static int MondayFirst(int dayOfWeekSun0ToSat6)
        {
            // Pazartesi=0 … Pazar=6
            return (dayOfWeekSun0ToSat6 + 6) % 7;
        }

        public async Task<ScheduleEntry> AddAsync(int userId, string recurrence, int? dayOfWeek, int? dayOfMonth, int startMinute, int endMinute, string title, int? topicId)
        {
            if (topicId.HasValue)
            {
                var inList = await _db.UserTopics.AnyAsync(ut => ut.UserId == userId && ut.TopicId == topicId.Value);
                if (!inList)
                    throw new InvalidOperationException("Seçilen konu listenizde yok. Önce Konular’dan ekleyin.");
            }

            var entry = new ScheduleEntry
            {
                UserId = userId,
                Recurrence = NormalizeRecurrence(recurrence),
                DayOfWeek = dayOfWeek,
                DayOfMonth = dayOfMonth,
                StartMinute = startMinute,
                EndMinute = endMinute,
                Title = title.Trim(),
                TopicId = topicId,
            };
            await _repository.AddAsync(entry);
            await _repository.SaveChangesAsync();
            return entry;
        }

        public async Task UpdateAsync(int userId, int id, string recurrence, int? dayOfWeek, int? dayOfMonth, int startMinute, int endMinute, string title, int? topicId)
        {
            if (topicId.HasValue)
            {
                var inList = await _db.UserTopics.AnyAsync(ut => ut.UserId == userId && ut.TopicId == topicId.Value);
                if (!inList)
                    throw new InvalidOperationException("Seçilen konu listenizde yok. Önce Konular’dan ekleyin.");
            }

            var existing = (await _repository.FindForReadAsync(e => e.Id == id && e.UserId == userId)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Program kaydı bulunamadı.");

            existing.Recurrence = NormalizeRecurrence(recurrence);
            existing.DayOfWeek = dayOfWeek;
            existing.DayOfMonth = dayOfMonth;
            existing.StartMinute = startMinute;
            existing.EndMinute = endMinute;
            existing.Title = title.Trim();
            existing.TopicId = topicId;
            _repository.Update(existing);
            await _repository.SaveChangesAsync();
        }

        public async Task DeleteAsync(int userId, int id)
        {
            var existing = (await _repository.FindForReadAsync(e => e.Id == id && e.UserId == userId)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Program kaydı bulunamadı.");

            _repository.Remove(existing);
            await _repository.SaveChangesAsync();
        }

        private static string NormalizeRecurrence(string r)
        {
            if (string.Equals(r, "Monthly", StringComparison.OrdinalIgnoreCase))
                return "Monthly";
            return "Weekly";
        }
    }
}
