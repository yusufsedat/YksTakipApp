using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Data;

/// <summary>
/// YKS TYT/AYT müfredat konularını veritabanına yükler.
/// <see cref="ResetCatalog"/> true iken tüm <see cref="UserTopic"/> ve <see cref="Topic"/> kayıtları silinir, ardından müfredat baştan eklenir.
/// Aksi halde yalnızca eksik (Category, Subject, Name) satırları eklenir.
/// </summary>
public static class YksCurriculumSeed
{
    public static async Task EnsureAsync(AppDbContext db, ILogger logger, IConfiguration config, CancellationToken ct = default)
    {
        var reset = config.GetValue<bool>("YksCurriculum:ResetCatalog");
        if (reset)
            await ResetAndSeedAsync(db, logger, ct);
        else
            await MergeEnsureAsync(db, logger, ct);
    }

    private static async Task ResetAndSeedAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var utCount = await db.UserTopics.AsNoTracking().CountAsync(ct);
        var tCount = await db.Topics.AsNoTracking().CountAsync(ct);

        await db.UserTopics.ExecuteDeleteAsync(ct);
        await db.Topics.ExecuteDeleteAsync(ct);

        logger.LogWarning(
            "YKS müfredat sıfırlandı (ResetCatalog): {Ut} kullanıcı-konu ve {T} katalog konusu silindi.",
            utCount,
            tCount);

        var items = GetNormalizedUniqueItems();
        var previousAutoDetect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await db.Topics.AddRangeAsync(items.Select(static x => new Topic
            {
                Category = x.Category,
                Subject = x.Subject,
                Name = x.Name
            }), ct);
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetect;
        }

        logger.LogInformation("YKS müfredat: {Count} konu baştan yüklendi.", items.Count);
    }

    private static async Task MergeEnsureAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var existingKeys = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var row in await db.Topics
                     .AsNoTracking()
                     .Select(t => new { t.Category, t.Subject, t.Name })
                     .ToListAsync(ct))
        {
            existingKeys.Add(Key(row.Category, row.Subject, row.Name));
        }

        var added = 0;
        var toAdd = new List<Topic>();
        foreach (var (cat, subj, name) in GetNormalizedUniqueItems())
        {
            var key = Key(cat, subj, name);
            if (existingKeys.Contains(key))
                continue;

            toAdd.Add(new Topic { Category = cat, Subject = subj, Name = name });
            existingKeys.Add(key);
            added++;
        }

        if (added > 0)
        {
            await db.Topics.AddRangeAsync(toAdd, ct);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("YKS müfredat: {Count} yeni konu eklendi.", added);
        }
        else
        {
            logger.LogInformation("YKS müfredat: güncel (yeni konu yok).");
        }
    }

    private static string Key(string cat, string subj, string name) =>
        $"{cat}\u001f{subj}\u001f{name}";

    private static IReadOnlyList<(string Category, string Subject, string Name)> GetNormalizedUniqueItems()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<(string Category, string Subject, string Name)>(CurriculumItems.Length);
        foreach (var (cat, subj, name) in CurriculumItems)
        {
            var c = cat.Trim();
            var s = subj.Trim();
            var n = name.Trim();
            if (c.Length == 0 || s.Length == 0 || n.Length == 0)
                continue;

            var key = Key(c, s, n);
            if (!seen.Add(key))
                continue;

            normalized.Add((c, s, n));
        }
        return normalized;
    }

    private static readonly (string Category, string Subject, string Name)[] CurriculumItems =
    [
        // ——— TYT — Türkçe ———
        ("TYT", "Türkçe", "Sözcükte Anlam"),
        ("TYT", "Türkçe", "Cümlede Anlam"),
        ("TYT", "Türkçe", "Paragraf"),
        ("TYT", "Türkçe", "Sözel Mantık"),
        ("TYT", "Türkçe", "Ses Bilgisi"),
        ("TYT", "Türkçe", "Yazım Kuralları"),
        ("TYT", "Türkçe", "Noktalama İşaretleri"),
        ("TYT", "Türkçe", "Dil Bilgisi (Temel düzey)"),
        // ——— TYT — Matematik ———
        ("TYT", "Matematik", "Temel Kavramlar"),
        ("TYT", "Matematik", "Sayılar"),
        ("TYT", "Matematik", "Bölme ve Bölünebilme"),
        ("TYT", "Matematik", "OBEB - OKEK"),
        ("TYT", "Matematik", "Rasyonel Sayılar"),
        ("TYT", "Matematik", "Basit Eşitsizlikler"),
        ("TYT", "Matematik", "Mutlak Değer"),
        ("TYT", "Matematik", "Üslü Sayılar"),
        ("TYT", "Matematik", "Köklü Sayılar"),
        ("TYT", "Matematik", "Çarpanlara Ayırma"),
        ("TYT", "Matematik", "Oran - Orantı"),
        ("TYT", "Matematik", "Problemler (Yaş, Kar-Zarar, İşçi, Hareket vb.)"),
        ("TYT", "Matematik", "Kümeler"),
        ("TYT", "Matematik", "Fonksiyon (Temel)"),
        ("TYT", "Matematik", "Veri - Grafik - Tablo"),
        // ——— TYT — Geometri ———
        ("TYT", "Geometri", "Doğruda Açılar"),
        ("TYT", "Geometri", "Üçgenler"),
        ("TYT", "Geometri", "Çokgenler"),
        ("TYT", "Geometri", "Dörtgenler"),
        ("TYT", "Geometri", "Çember ve Daire"),
        ("TYT", "Geometri", "Katı Cisimler (Temel)"),
        // ——— TYT — Sosyal ———
        ("TYT", "Tarih", "Tarih Bilimine Giriş"),
        ("TYT", "Tarih", "İlk Çağ Uygarlıkları"),
        ("TYT", "Tarih", "İslamiyet Öncesi Türk Tarihi"),
        ("TYT", "Tarih", "İslam Tarihi"),
        ("TYT", "Tarih", "Osmanlı Tarihi"),
        ("TYT", "Tarih", "Kurtuluş Savaşı"),
        ("TYT", "Tarih", "Atatürk İlke ve İnkılapları"),
        ("TYT", "Coğrafya", "Harita Bilgisi"),
        ("TYT", "Coğrafya", "İklim"),
        ("TYT", "Coğrafya", "Yer Şekilleri"),
        ("TYT", "Coğrafya", "Nüfus ve Yerleşme"),
        ("TYT", "Coğrafya", "Türkiye Coğrafyası"),
        ("TYT", "Felsefe", "Felsefenin Konusu"),
        ("TYT", "Felsefe", "Bilgi Felsefesi"),
        ("TYT", "Felsefe", "Varlık Felsefesi"),
        ("TYT", "Felsefe", "Ahlak Felsefesi"),
        ("TYT", "Din Kültürü", "İslam Bilgisi"),
        ("TYT", "Din Kültürü", "İnanç Konuları"),
        ("TYT", "Din Kültürü", "Ahlak"),
        ("TYT", "Din Kültürü", "Din ve Hayat"),
        // ——— TYT — Fen ———
        ("TYT", "Fizik", "Fizik Bilimine Giriş"),
        ("TYT", "Fizik", "Madde ve Özellikleri"),
        ("TYT", "Fizik", "Kuvvet ve Hareket"),
        ("TYT", "Fizik", "Enerji"),
        ("TYT", "Fizik", "Isı ve Sıcaklık"),
        ("TYT", "Fizik", "Basınç"),
        ("TYT", "Kimya", "Kimya Bilimi"),
        ("TYT", "Kimya", "Atom ve Periyodik Sistem"),
        ("TYT", "Kimya", "Kimyasal Türler"),
        ("TYT", "Kimya", "Maddenin Halleri"),
        ("TYT", "Kimya", "Karışımlar"),
        ("TYT", "Biyoloji", "Canlıların Ortak Özellikleri"),
        ("TYT", "Biyoloji", "Hücre"),
        ("TYT", "Biyoloji", "Canlıların Sınıflandırılması"),
        ("TYT", "Biyoloji", "Ekosistem"),
        // ——— AYT — Matematik ———
        ("AYT", "AYT Matematik", "Matematik"),
        ("AYT", "AYT Matematik", "Fonksiyonlar"),
        ("AYT", "AYT Matematik", "Polinomlar"),
        ("AYT", "AYT Matematik", "Dereceden Denklemler"),
        ("AYT", "AYT Matematik", "Permütasyon - Kombinasyon"),
        ("AYT", "AYT Matematik", "Binom"),
        ("AYT", "AYT Matematik", "Olasılık"),
        ("AYT", "AYT Matematik", "Logaritma"),
        ("AYT", "AYT Matematik", "Diziler"),
        ("AYT", "AYT Matematik", "Limit"),
        ("AYT", "AYT Matematik", "Türev"),
        ("AYT", "AYT Matematik", "İntegral"),
        // ——— AYT — Geometri ———
        ("AYT", "AYT Geometri", "Geometri"),
        ("AYT", "AYT Geometri", "Üçgenler (İleri düzey)"),
        ("AYT", "AYT Geometri", "Analitik Geometri"),
        ("AYT", "AYT Geometri", "Çember ve Daire"),
        ("AYT", "AYT Geometri", "Katı Cisimler"),
        ("AYT", "AYT Geometri", "Vektörler"),
        // ——— AYT — Fen ———
        ("AYT", "AYT Fizik", "Vektörler"),
        ("AYT", "AYT Fizik", "Kuvvet ve Hareket"),
        ("AYT", "AYT Fizik", "Elektrik ve Manyetizma"),
        ("AYT", "AYT Fizik", "Dalgalar"),
        ("AYT", "AYT Fizik", "Optik"),
        ("AYT", "AYT Fizik", "Modern Fizik"),
        ("AYT", "AYT Kimya", "Modern Atom Teorisi"),
        ("AYT", "AYT Kimya", "Gazlar"),
        ("AYT", "AYT Kimya", "Çözeltiler"),
        ("AYT", "AYT Kimya", "Kimyasal Tepkimeler"),
        ("AYT", "AYT Kimya", "Kimyasal Denge"),
        ("AYT", "AYT Kimya", "Asit - Baz"),
        ("AYT", "AYT Kimya", "Elektrokimya"),
        ("AYT", "AYT Kimya", "Organik Kimya"),
        ("AYT", "AYT Biyoloji", "Hücre Bölünmeleri"),
        ("AYT", "AYT Biyoloji", "Kalıtım"),
        ("AYT", "AYT Biyoloji", "Evrim"),
        ("AYT", "AYT Biyoloji", "Sistemler (Sindirim, Dolaşım vb.)"),
        ("AYT", "AYT Biyoloji", "Bitki Biyolojisi"),
        ("AYT", "AYT Biyoloji", "Ekoloji"),
        // ——— AYT — Eşit ağırlık / Sözel ———
        ("AYT", "AYT Edebiyat", "Edebiyat Akımları"),
        ("AYT", "AYT Edebiyat", "Şiir Bilgisi"),
        ("AYT", "AYT Edebiyat", "Tanzimat"),
        ("AYT", "AYT Edebiyat", "Servet-i Fünun"),
        ("AYT", "AYT Edebiyat", "Cumhuriyet Dönemi"),
        ("AYT", "AYT Edebiyat", "Yazar - Eser"),
        ("AYT", "AYT Tarih", "Türk-İslam Devletleri"),
        ("AYT", "AYT Tarih", "Osmanlı Devleti"),
        ("AYT", "AYT Tarih", "İnkılap Tarihi"),
        ("AYT", "AYT Tarih", "Çağdaş Dünya"),
        ("AYT", "AYT Coğrafya", "Türkiye Coğrafyası"),
        ("AYT", "AYT Coğrafya", "Ekonomik Faaliyetler"),
        ("AYT", "AYT Coğrafya", "Nüfus"),
        ("AYT", "AYT Coğrafya", "Çevre ve Toplum"),
    ];
}
