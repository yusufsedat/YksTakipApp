namespace YksTakipApp.Application.Options;

public sealed class AdaptationPolicyOptions
{
    public const string SectionName = "AdaptationPolicy";

    public int LowScoreThresholdPercent { get; set; } = 20;
    public int HighScoreThresholdPercent { get; set; } = 75;
    public double LowScoreConfidencePenalty { get; set; } = 0.30;
    public double HighScoreConfidenceGain { get; set; } = 0.20;
    public double LockThreshold { get; set; } = 0.25;
    public double UnlockThreshold { get; set; } = 0.70;
}
