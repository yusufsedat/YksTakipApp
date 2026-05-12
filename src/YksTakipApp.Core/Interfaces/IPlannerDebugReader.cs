using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

public interface IPlannerDebugReader
{
    Task<PlannerDebugSnapshot?> GetAsync(int userId, CancellationToken ct = default);
}
