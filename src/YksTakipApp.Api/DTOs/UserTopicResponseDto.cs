namespace YksTakipApp.Api.DTOs;

/// <summary>Mobil konu listesi: <see cref="YksTakipApp.Core.Entities.TopicStatus"/> sayısal (0–3).</summary>
public sealed class UserTopicResponseDto
{
    public int UserId { get; set; }
    public int TopicId { get; set; }
    public int Status { get; set; }
    public string MasteryStatus { get; set; } = "";
    public double MasteryConfidence { get; set; }
    public bool IsLocked { get; set; }
}
