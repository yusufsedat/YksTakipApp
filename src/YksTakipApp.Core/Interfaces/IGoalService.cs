using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces
{
    public interface IGoalService
    {
        /// <summary>Kullanıcı yoksa null.</summary>
        Task<GoalStatusResult?> GetStatusAsync(int userId);

        Task<UserGoalSnapshot> CreateAsync(
            int userId,
            string targetUniversity,
            string targetDepartment,
            decimal? targetTytNet,
            decimal? targetAytNet,
            int dailyAvailableMinutes);

        /// <summary>Yeni skip sayısı. Skip limiti veya aktif hedef için <see cref="InvalidOperationException"/>.</summary>
        Task<int> SkipAsync(int userId);
    }
}
