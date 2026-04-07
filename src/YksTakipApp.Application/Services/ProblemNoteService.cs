using System.Text.Json;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Application.Services
{
    public class ProblemNoteService : IProblemNoteService
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly IRepository<ProblemNote> _repository;
        private readonly IProblemNoteImageStorage _imageStorage;

        public ProblemNoteService(IRepository<ProblemNote> repository, IProblemNoteImageStorage imageStorage)
        {
            _repository = repository;
            _imageStorage = imageStorage;
        }

        public async Task<IReadOnlyList<ProblemNote>> ListAsync(int userId)
        {
            var items = (await _repository.FindAsync(x => x.UserId == userId && !x.IsDeleted)).ToList();
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
            var uploaded = await _imageStorage.UploadAsync(userId, imageBase64);
            var entry = new ProblemNote
            {
                UserId = userId,
                ImageUrl = uploaded.SecureUrl,
                ImagePublicId = uploaded.PublicId,
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
            var existing = (await _repository.FindAsync(x => x.Id == id && x.UserId == userId && !x.IsDeleted)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Not bulunamadı.");

            existing.TagsJson = SerializeTags(tags);
            existing.SolutionLearned = solutionLearned;

            if (!string.IsNullOrWhiteSpace(imageBase64))
            {
                await _imageStorage.DeleteAsync(existing.ImagePublicId);
                var uploaded = await _imageStorage.UploadAsync(userId, imageBase64);
                existing.ImageUrl = uploaded.SecureUrl;
                existing.ImagePublicId = uploaded.PublicId;
            }

            _repository.Update(existing);
            await _repository.SaveChangesAsync();
        }

        public async Task DeleteAsync(int userId, int id)
        {
            var existing = (await _repository.FindAsync(x => x.Id == id && x.UserId == userId && !x.IsDeleted)).FirstOrDefault();
            if (existing is null)
                throw new InvalidOperationException("Not bulunamadı.");

            await _imageStorage.DeleteAsync(existing.ImagePublicId);
            existing.IsDeleted = true;
            _repository.Update(existing);
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
