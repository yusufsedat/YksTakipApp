namespace YksTakipApp.Api.DTOs;

public sealed class GenerateWeeklyPlanRequest
{
    public DateOnly StartDate { get; set; }
    public string? ClientRequestId { get; set; }
}
