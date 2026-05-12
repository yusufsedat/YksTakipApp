namespace YksTakipApp.Api.DTOs;

public sealed class PlannerWeeklyQuery
{
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
}
