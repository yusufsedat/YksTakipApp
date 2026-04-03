using System.Text.Json;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class ProblemNoteService : IProblemNoteService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly IRepository<ProblemNote> _repository;

        public ProblemNoteService(IRepository<ProblemNote> repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<ProblemNote>> ListAsync(int userId)
        {
            var items = (await _repository.FindAsync(x => x.UserId == userId)).ToList();
            items.Sort(static (a, b) =>
            {
                var c = a.SolutionLearned.CompareTo(b.SolutionLearned);
                if (c != 0) return c;
                return b.CreatedAt.CompareTo(a.CreatedAt);
            });
            return items;
        }

        public async Task<ProblemNote> AddAsync(int userId, string imageBase64, IReadOnlyList<string> tags, bool solutionLearned)
        {
            var entry = new ProblemNote
            {
                UserId = userId,
                ImageBase64 = imageBase64.Trim(),
                TagsJson = SerializeTags(tags),
                SolutionLearned = solutionLearned,
                CreatedAt = DateTime.UtcNow,
            };
            await _repository.AddAsync(entry);
            await _repository.SaveChangesAsync();
            return entry;
        }

        public async Task UpdateAsync(int userId, int id, IReadOnlyList<string> tags, bool solutionLearned, string? imageBase64)
        {
            var existing = (await _repository.FindAsync(x => x.Id == id && x.UserId == userId)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Not bulunamadı.");

            existing.TagsJson = SerializeTags(tags);
            existing.SolutionLearned = solutionLearned;
            if (!string.IsNullOrWhiteSpace(imageBase64))
                existing.ImageBase64 = imageBase64.Trim();

            _repository.Update(existing);
            await _repository.SaveChangesAsync();
        }

        public async Task DeleteAsync(int userId, int id)
        {
            var existing = (await _repository.FindAsync(x => x.Id == id && x.UserId == userId)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Not bulunamadı.");

            _repository.Remove(existing);
            await _repository.SaveChangesAsync();
        }

        private static string SerializeTags(IReadOnlyList<string> tags)
        {
            var cleaned = tags
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(25)
                .ToList();
            return JsonSerializer.Serialize(cleaned, JsonOpts);
        }
    }
}
