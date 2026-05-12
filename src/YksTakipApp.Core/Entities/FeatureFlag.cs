namespace YksTakipApp.Core.Entities;

/// <summary>
/// Ürün/deney kontrolü için flag. Notification preference karıştırılmaz.
/// </summary>
public sealed class FeatureFlag
{
    /// <summary>Stable string PK; ör. "dynamicBuffer.enabled".</summary>
    public string Key { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string? Description { get; set; }

    /// <summary>0-100 arasında. 0 -> hiç kimseye açık değil; 100 -> herkese açık.</summary>
    public int RolloutPercentage { get; set; } = 100;

    /// <summary>Whitelist: null, "new_user", "active_user", "beta". JSON rule engine yok.</summary>
    public string? Segment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
