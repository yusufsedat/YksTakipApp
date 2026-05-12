using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Core.Models;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class NotificationPolicyService : INotificationPolicyService
{
    private readonly AppDbContext _db;

    public NotificationPolicyService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<NotificationPayload>> PreviewDailyNotificationsAsync(int userId, DateOnly day, CancellationToken ct = default)
    {
        var result = new List<NotificationPayload>();
        var goal = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Join(_db.UserGoals.AsNoTracking(), u => u.ActiveGoalVersionId, g => g.Id, (_, g) => g)
            .FirstOrDefaultAsync(ct);
        var dailyTarget = goal?.DailyAvailableMinutes ?? 120;
        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var todayMinutes = await _db.StudyTimes.AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= dayStart && s.Date < dayEnd)
            .SumAsync(s => (int?)s.DurationMinutes, ct) ?? 0;

        if (dailyTarget - todayMinutes is > 0 and <= 30)
            result.Add(new NotificationPayload("capacity_close", "Hedefe cok az kaldi", $"Bugun hedefine ulasmana {dailyTarget - todayMinutes} dakika kaldi.", new Dictionary<string, string> { ["remainingMinutes"] = (dailyTarget - todayMinutes).ToString() }));
        if (todayMinutes >= dailyTarget)
            result.Add(new NotificationPayload("capacity_done", "Hedef tamamlandi", "Bugunku hedefini tamamladin, harika!", new Dictionary<string, string>()));

        var yesterday = day.AddDays(-1);
        var yStart = yesterday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var yEnd = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var yesterdayMinutes = await _db.StudyTimes.AsNoTracking()
            .Where(s => s.UserId == userId && s.Date >= yStart && s.Date < yEnd)
            .SumAsync(s => (int?)s.DurationMinutes, ct) ?? 0;
        if (yesterdayMinutes < dailyTarget)
            result.Add(new NotificationPayload("yesterday_under_target", "Dun hedefin altinda kaldi", "Dun hedefinin altinda kaldin, bugun kisa bir tekrar ekleyelim.", new Dictionary<string, string>()));

        var existingTypes = await _db.UserNotificationLogs.AsNoTracking()
            .Where(n => n.UserId == userId && n.TargetDate == day)
            .Select(n => n.NotificationType)
            .ToListAsync(ct);
        return result.Where(r => !existingTypes.Contains(r.Type)).ToList();
    }

    public static string SerializePayload(NotificationPayload payload) => JsonSerializer.Serialize(payload);
}
