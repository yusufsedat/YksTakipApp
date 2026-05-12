namespace YksTakipApp.Core.Entities;

/// <summary>
/// Kullanıcı bildirim tercihleri. Feature flag ile karıştırılmaz.
/// </summary>
public sealed class UserNotificationPreference
{
    public int UserId { get; set; }
    public bool DailyReminderEnabled { get; set; } = true;
    public bool RecoveryReminderEnabled { get; set; } = true;
    public bool WeeklyReviewEnabled { get; set; } = true;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
