namespace YksTakipApp.Core.Entities;

public sealed class UserNotificationLog
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string NotificationType { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string PayloadJson { get; set; } = "{}";
    public DateOnly TargetDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
