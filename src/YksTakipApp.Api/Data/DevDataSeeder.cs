using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Data;

/// <summary>
/// Sadece Development ortamında, veritabanında demo kullanıcı yoksa bir kez örnek veri ekler.
/// Konular global katalogdan (YksCurriculumSeed) seçilir; ayrıca sahte konu satırı eklenmez.
/// Demo giriş: demo@ykstakip.local / Demo123!
/// </summary>
public static class DevDataSeeder
{
    private const string DemoEmail = "demo@ykstakip.local";
    private const string DemoPassword = "Demo123!";
    private const string DemoName = "Demo Öğrenci";

    /// <summary>Örnek liste: müfredattaki (Category, Subject, Name) ile birebir eşleşmeli.</summary>
    private static readonly (string Category, string Subject, string Name)[] DemoTopicPicks =
    [
        ("TYT", "Türkçe", "Paragraf"),
        ("TYT", "Matematik", "Temel Kavramlar"),
        ("TYT", "Fizik", "Fizik Bilimine Giriş"),
        ("TYT", "Tarih", "Osmanlı Tarihi"),
        ("AYT", "AYT Matematik", "Limit"),
        ("AYT", "AYT Fizik", "Optik"),
    ];

    private static readonly TopicStatus[] DemoTopicStatuses =
    [
        TopicStatus.Completed,
        TopicStatus.InProgress,
        TopicStatus.NotStarted,
        TopicStatus.NeedsReview,
        TopicStatus.InProgress,
        TopicStatus.Completed,
    ];

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == DemoEmail, ct);
        if (existingUser is not null)
        {
            await EnsureDemoUserSampleTopicsAsync(db, existingUser.Id, logger, ct);
            logger.LogInformation("Dev seed atlandı: {Email} zaten var.", DemoEmail);
            return;
        }

        logger.LogInformation("Dev mock verisi ekleniyor…");

        var user = new User
        {
            Name = DemoName,
            Email = DemoEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
            Role = "User",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var uid = user.Id;
        await AddDemoUserSampleTopicsAsync(db, uid, logger, ct);

        var today = DateTime.UtcNow.Date;
        var studyRows = new List<StudyTime>
        {
            new() { UserId = uid, Date = today.AddDays(-1).AddHours(12), DurationMinutes = 90 },
            new() { UserId = uid, Date = today.AddDays(-2).AddHours(12), DurationMinutes = 45 },
            new() { UserId = uid, Date = today.AddDays(-3).AddHours(12), DurationMinutes = 120 },
            new() { UserId = uid, Date = today.AddDays(-4).AddHours(12), DurationMinutes = 60 },
            new() { UserId = uid, Date = today.AddDays(-5).AddHours(12), DurationMinutes = 75 },
            new() { UserId = uid, Date = today.AddDays(-6).AddHours(12), DurationMinutes = 100 },
            new() { UserId = uid, Date = today.AddDays(-7).AddHours(12), DurationMinutes = 30 },
        };
        db.StudyTimes.AddRange(studyRows);

        db.ExamResults.AddRange(
            new ExamResult
            {
                UserId = uid,
                ExamName = "TYT Genel Deneme 1",
                ExamType = "TYT",
                Date = today.AddDays(-3).AddHours(12),
                NetTyt = 38.5,
                NetAyt = 0,
            },
            new ExamResult
            {
                UserId = uid,
                ExamName = "AYT Sayısal Deneme",
                ExamType = "AYT",
                Date = today.AddDays(-10).AddHours(12),
                NetTyt = 40,
                NetAyt = 32.25,
            },
            new ExamResult
            {
                UserId = uid,
                ExamName = "TYT Genel Deneme 2",
                ExamType = "TYT",
                Date = today.AddDays(-14).AddHours(12),
                NetTyt = 36,
                NetAyt = 0,
            },
            new ExamResult
            {
                UserId = uid,
                ExamName = "Haftalık Branş — Matematik",
                ExamType = "TYT",
                Date = today.AddDays(-1).AddHours(12),
                NetTyt = 35,
                NetAyt = 28.5,
            }
        );

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Dev mock verisi tamam. Mobil/API ile giriş: {Email}", DemoEmail);
    }

    private static async Task EnsureDemoUserSampleTopicsAsync(AppDbContext db, int userId, ILogger logger, CancellationToken ct)
    {
        if (await db.UserTopics.AnyAsync(ut => ut.UserId == userId, ct))
            return;
        await AddDemoUserSampleTopicsAsync(db, userId, logger, ct);
    }

    private static async Task AddDemoUserSampleTopicsAsync(AppDbContext db, int userId, ILogger logger, CancellationToken ct)
    {
        var n = Math.Min(DemoTopicPicks.Length, DemoTopicStatuses.Length);
        for (var i = 0; i < n; i++)
        {
            var (cat, sub, name) = DemoTopicPicks[i];
            var topicId = await db.Topics
                .Where(t => t.Category == cat && t.Subject == sub && t.Name == name)
                .Select(t => t.Id)
                .FirstOrDefaultAsync(ct);

            if (topicId == 0)
            {
                logger.LogWarning("Demo konusu katalogda yok (YksCurriculumSeed önce çalışmalı): {Cat} / {Sub} / {Name}", cat, sub, name);
                continue;
            }

            db.UserTopics.Add(new UserTopic
            {
                UserId = userId,
                TopicId = topicId,
                Status = DemoTopicStatuses[i],
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
