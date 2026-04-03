using Microsoft.EntityFrameworkCore;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Data;

/// <summary>
/// Development ortamında tek seferlik admin hesabı. Giriş: admin@ykstakip.local / Admin123!
/// </summary>
public static class AdminDevSeeder
{
    public const string AdminEmail = "admin@ykstakip.local";
    public const string AdminPassword = "Admin123!";

    public static async Task EnsureAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == AdminEmail, ct);

        if (existing is null)
        {
            db.Users.Add(new User
            {
                Name = "Admin",
                Email = AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Admin hesabı eklendi: {Email}", AdminEmail);
            return;
        }

        // Kolonlar sonradan eklendiyse (migration default = "User") mevcut satırda Role yanlış kalabilir.
        // Admin’i her zaman Admin rolü ile güncelliyoruz.
        var updated = false;
        if (!string.Equals(existing.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            existing.Role = "Admin";
            updated = true;
        }

        if (!string.Equals(existing.Name, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            existing.Name = "Admin";
            updated = true;
        }

        // Şifre hash'i de admin password değiştiyse güncellensin istersen:
        // (Yüksek güvenlik için prod'da kapalı; sadece dev seed için.)
        existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword);
        updated = true;

        if (updated)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Admin hesabı güncellendi: {Email}", AdminEmail);
        }
    }
}
