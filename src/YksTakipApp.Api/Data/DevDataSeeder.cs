using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Data;

/// <summary>
/// Sadece Development ortamında, veritabanında demo kullanıcı yoksa bir kez örnek veri ekler.
/// Konular migration ile yönetilen global katalogdan seçilir; ayrıca sahte konu satırı eklenmez.
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
        await SeedFeatureFlagsAsync(db, logger, ct);

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == DemoEmail, ct);
        if (existingUser is not null)
        {
            await EnsureDemoUserSampleTopicsAsync(db, existingUser.Id, logger, ct);
            await SeedAdaptiveScenarioAsync(db, existingUser.Id, logger, ct);
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
        await SeedAdaptiveScenarioAsync(db, uid, logger, ct);

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
                logger.LogWarning("Demo konusu katalogda yok (topics seed migration uygulanmalı): {Cat} / {Sub} / {Name}", cat, sub, name);
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

    private static async Task SeedAdaptiveScenarioAsync(AppDbContext db, int userId, ILogger logger, CancellationToken ct)
    {
        var topicPool = await db.Topics
            .AsNoTracking()
            .Where(t => t.Subject.Contains("Matematik") || t.Subject.Contains("Fizik"))
            .OrderBy(t => t.Subject)
            .ThenBy(t => t.Id)
            .Take(50)
            .ToListAsync(ct);

        if (topicPool.Count < 30)
        {
            logger.LogWarning("Adaptation seed atlandı: Matematik/Fizik havuzunda 30'dan az konu var.");
            return;
        }

        var selected = topicPool.Take(40).ToList();
        await EnsurePrerequisiteGraphAsync(db, selected, logger, ct);
        await EnsureAdaptiveUserTopicsAsync(db, userId, selected, ct);
        await EnsureAdaptiveStudyHistoryAsync(db, userId, selected, ct);
        await EnsureAdaptiveExamHistoryAsync(db, userId, ct);
    }

    private static async Task EnsurePrerequisiteGraphAsync(AppDbContext db, List<Topic> selected, ILogger logger, CancellationToken ct)
    {
        var topicIds = selected.Select(t => t.Id).ToList();
        var existing = await db.TopicPrerequisites
            .Where(x => topicIds.Contains(x.TopicId) && topicIds.Contains(x.PrerequisiteTopicId))
            .Select(x => new { x.TopicId, x.PrerequisiteTopicId })
            .ToListAsync(ct);

        var existingSet = existing
            .Select(x => (x.TopicId, x.PrerequisiteTopicId))
            .ToHashSet();

        var toAdd = new List<TopicPrerequisite>();
        for (var i = 1; i < selected.Count; i++)
        {
            var edge = (TopicId: selected[i].Id, PrerequisiteTopicId: selected[i - 1].Id);
            if (!existingSet.Contains(edge))
                toAdd.Add(new TopicPrerequisite { TopicId = edge.TopicId, PrerequisiteTopicId = edge.PrerequisiteTopicId });
        }

        for (var i = 2; i < selected.Count; i += 3)
        {
            var edge = (TopicId: selected[i].Id, PrerequisiteTopicId: selected[i - 2].Id);
            if (!existingSet.Contains(edge))
                toAdd.Add(new TopicPrerequisite { TopicId = edge.TopicId, PrerequisiteTopicId = edge.PrerequisiteTopicId });
        }

        if (toAdd.Count == 0)
            return;

        db.TopicPrerequisites.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Adaptation seed: {Count} prerequisite edge eklendi.", toAdd.Count);
    }

    private static async Task EnsureAdaptiveUserTopicsAsync(AppDbContext db, int userId, List<Topic> selected, CancellationToken ct)
    {
        var selectedIds = selected.Select(s => s.Id).ToList();
        var existingIds = await db.UserTopics
            .Where(ut => ut.UserId == userId && selectedIds.Contains(ut.TopicId))
            .Select(ut => ut.TopicId)
            .ToListAsync(ct);

        var existingSet = existingIds.ToHashSet();
        var toAdd = new List<UserTopic>();
        for (var i = 0; i < selected.Count; i++)
        {
            var topicId = selected[i].Id;
            if (existingSet.Contains(topicId))
                continue;

            var status = (i % 5) switch
            {
                0 => TopicStatus.NotStarted,
                1 => TopicStatus.InProgress,
                2 => TopicStatus.NeedsReview,
                3 => TopicStatus.InProgress,
                _ => TopicStatus.NotStarted
            };

            toAdd.Add(new UserTopic
            {
                UserId = userId,
                TopicId = topicId,
                Status = status
            });
        }

        if (toAdd.Count > 0)
        {
            db.UserTopics.AddRange(toAdd);
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureAdaptiveStudyHistoryAsync(AppDbContext db, int userId, List<Topic> selected, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-14);
        var existing = await db.StudyTimes.AnyAsync(s => s.UserId == userId && s.Date >= from, ct);
        if (existing)
            return;

        var rng = new Random(42);
        var studyRows = new List<StudyTime>();
        for (var day = 1; day <= 14; day++)
        {
            var sessionCount = 1 + rng.Next(0, 3);
            for (var i = 0; i < sessionCount; i++)
            {
                var topic = selected[rng.Next(selected.Count)];
                studyRows.Add(new StudyTime
                {
                    UserId = userId,
                    TopicId = topic.Id,
                    Date = today.AddDays(-day).AddHours(10 + i * 3),
                    DurationMinutes = 20 + rng.Next(15, 70)
                });
            }
        }

        db.StudyTimes.AddRange(studyRows);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureAdaptiveExamHistoryAsync(AppDbContext db, int userId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-21);
        var existing = await db.ExamResults.AnyAsync(e => e.UserId == userId && e.Date >= from, ct);
        if (existing)
            return;

        var exams = new List<ExamResult>
        {
            new()
            {
                UserId = userId,
                ExamName = "TYT Genel Deneme — Zayıf Seri 1",
                ExamType = "TYT",
                Date = today.AddDays(-2).AddHours(12),
                NetTyt = 34.75,
                NetAyt = 0,
                ExamDetails =
                [
                    new ExamDetail { Subject = "Matematik", Correct = 3, Wrong = 17, Blank = 20 },
                    new ExamDetail { Subject = "Fizik", Correct = 2, Wrong = 9, Blank = 9 }
                ]
            },
            new()
            {
                UserId = userId,
                ExamName = "TYT Genel Deneme — Zayıf Seri 2",
                ExamType = "TYT",
                Date = today.AddDays(-6).AddHours(12),
                NetTyt = 37.25,
                NetAyt = 0,
                ExamDetails =
                [
                    new ExamDetail { Subject = "Matematik", Correct = 4, Wrong = 14, Blank = 22 },
                    new ExamDetail { Subject = "Fizik", Correct = 3, Wrong = 8, Blank = 9 }
                ]
            },
            new()
            {
                UserId = userId,
                ExamName = "AYT Sayısal Deneme — Zayıf Seri",
                ExamType = "AYT",
                Date = today.AddDays(-11).AddHours(12),
                NetTyt = 0,
                NetAyt = 30.5,
                ExamDetails =
                [
                    new ExamDetail { Subject = "AYT Matematik", Correct = 2, Wrong = 13, Blank = 25 },
                    new ExamDetail { Subject = "AYT Fizik", Correct = 2, Wrong = 8, Blank = 4 }
                ]
            },
            new()
            {
                UserId = userId,
                ExamName = "Branş Matematik — Kötü Seri",
                ExamType = "BRANS",
                Subject = "Matematik",
                Date = today.AddDays(-15).AddHours(12),
                NetTyt = 0,
                NetAyt = 0,
                ExamDetails =
                [
                    new ExamDetail { Subject = "Matematik", Correct = 4, Wrong = 16, Blank = 20 }
                ]
            }
        };

        db.ExamResults.AddRange(exams);
        await db.SaveChangesAsync(ct);
    }

    private static readonly (string Key, bool IsEnabled, string Description, string? Segment)[] InitialFlags =
    [
        ("dynamicBuffer.enabled", true, "Planner: gun ici dinamik buffer (yetkin user'lar icin azaltir).", null),
        ("aggressiveAdaptation.enabled", false, "Adaptation: agresif zorluk uyarlamasi.", null),
        ("churnAutoTrigger.enabled", true, "Planner: bos plan tespitinde otomatik tetik.", null),
        ("notifications.proactive.enabled", false, "Bildirim: proaktif/uyarici push'lar.", null),
        ("planner.reviewInjection.enabled", true, "Planner: review task injection.", null),
        ("planner.suppression.enabled", false, "Planner: rec suppression deneyi.", null),
    ];

    private static async Task SeedFeatureFlagsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var existingKeys = await db.FeatureFlags.AsNoTracking().Select(f => f.Key).ToListAsync(ct);
        var missing = InitialFlags.Where(f => !existingKeys.Contains(f.Key)).ToList();
        if (missing.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var f in missing)
        {
            db.FeatureFlags.Add(new FeatureFlag
            {
                Key = f.Key,
                IsEnabled = f.IsEnabled,
                Description = f.Description,
                Segment = f.Segment,
                RolloutPercentage = 100,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} feature flag(s).", missing.Count);
    }
}
