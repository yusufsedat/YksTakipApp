using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

/// <summary>
/// Planner çağrısı başına 1 satır PlannerDecisionLog yazar. Planner transaction'ından ayrı çalışır;
/// hata olursa swallow eder ama LogError structured field'larla atılır.
/// </summary>
public interface IPlannerDecisionLogger
{
    Task LogAsync(PlannerDecisionContext context, CancellationToken ct);
}
