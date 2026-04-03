using YksTakipApp.Core.Entities;

namespace YksTakipApp.Core.Interfaces
{
    public interface IProblemNoteService
    {
        Task<IReadOnlyList<ProblemNote>> ListAsync(int userId);
        Task<ProblemNote> AddAsync(int userId, string imageBase64, IReadOnlyList<string> tags, bool solutionLearned);
        Task UpdateAsync(int userId, int id, IReadOnlyList<string> tags, bool solutionLearned, string? imageBase64);
        Task DeleteAsync(int userId, int id);
    }
}
