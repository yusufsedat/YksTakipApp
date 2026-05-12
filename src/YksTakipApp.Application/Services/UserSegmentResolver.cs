using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YksTakipApp.Core.Enums;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

/// <summary>
/// Basit heuristik segment resolver. Whitelist: null, "new_user", "active_user", "beta".
/// 60s in-memory cache (per userId).
/// </summary>
public sealed class UserSegmentResolver : IUserSegmentResolver
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public UserSegmentResolver(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<string?> ResolveAsync(int userId, CancellationToken ct = default)
    {
        var cacheKey = $"segment:{userId}";
        if (_cache.TryGetValue(cacheKey, out string? cached))
            return cached;

        var resolved = await ResolveCoreAsync(userId, ct);
        _cache.Set(cacheKey, resolved, CacheTtl);
        return resolved;
    }

    private async Task<string?> ResolveCoreAsync(int userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.CreatedAt, u.Role })
            .FirstOrDefaultAsync(ct);
        if (user is null)
            return null;

        // beta segmenti placeholder: appsettings whitelist'ten gelmeli (ileride). Şimdilik
        // Role=="Admin" beta sayılmaz, beta whitelist'i devre dışı.

        var now = DateTime.UtcNow;
        var accountAgeDays = (now - user.CreatedAt).TotalDays;
        var weekAgo = now.AddDays(-7);

        var completedLastWeek = await _db.ScheduleTasks.AsNoTracking()
            .Where(t => t.UserId == userId
                        && t.Status == ScheduleTaskStatus.Completed
                        && t.TaskType == TaskType.Study
                        && t.UpdatedAt >= weekAgo)
            .CountAsync(ct);

        var isNewUser = accountAgeDays < 14;
        if (isNewUser && completedLastWeek < 2)
            return "new_user";
        if (!isNewUser && completedLastWeek >= 4)
            return "active_user";
        return null;
    }
}
