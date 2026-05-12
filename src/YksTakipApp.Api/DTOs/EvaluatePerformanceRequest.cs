namespace YksTakipApp.Api.DTOs;

public sealed class EvaluatePerformanceRequest
{
    public int TopicId { get; set; }
    /// <summary>0–100 yüzde (son deneme / konu skoru).</summary>
    public int RecentExamScorePercent { get; set; }
}
