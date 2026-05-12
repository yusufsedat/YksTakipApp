namespace YksTakipApp.Api.DTOs;

public sealed class FeatureFlagDto
{
    public string Key { get; init; } = "";
    public bool IsEnabled { get; init; }
    public string? Description { get; init; }
    public int RolloutPercentage { get; init; }
    public string? Segment { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class UpdateFeatureFlagRequest
{
    public bool? IsEnabled { get; init; }
    public int? RolloutPercentage { get; init; }
    public string? Segment { get; init; }
    public string? Description { get; init; }
}

public sealed class UpsertFlagOverrideRequest
{
    public int UserId { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
