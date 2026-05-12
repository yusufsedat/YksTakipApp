using YksTakipApp.Core.Models;

namespace YksTakipApp.Core.Interfaces;

public interface INotificationPolicyService
{
    Task<IReadOnlyList<NotificationPayload>> PreviewDailyNotificationsAsync(int userId, DateOnly day, CancellationToken ct = default);
}
