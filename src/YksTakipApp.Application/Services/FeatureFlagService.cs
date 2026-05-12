using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Application.Services;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan FlagCacheTtl = TimeSpan.FromSeconds(60);
    private const string FlagCacheKeyPrefix = "ff:";

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IUserSegmentResolver _segmentResolver;

    public FeatureFlagService(AppDbContext db, IMemoryCache cache, IUserSegmentResolver segmentResolver)
    {
        _db = db;
        _cache = cache;
        _segmentResolver = segmentResolver;
    }

    public async Task<bool> IsEnabledAsync(string key, int? userId, CancellationToken ct = default)
    {
        var flag = await GetFlagAsync(key, ct);
        if (flag is null)
            return false;

        if (userId is null)
            return flag.IsEnabled;

        // 1) user override
        var now = DateTime.UtcNow;
        var ov = await _db.UserFeatureFlagOverrides.AsNoTracking()
            .Where(o => o.UserId == userId.Value && o.FlagKey == key)
            .Select(o => new { o.IsEnabled, o.ExpiresAt })
            .FirstOrDefaultAsync(ct);
        if (ov is not null && (ov.ExpiresAt == null || ov.ExpiresAt > now))
            return ov.IsEnabled;

        // 2) segment match — segment varsa kullanıcının segment'i flag.segment ile eşleşmeli;
        // eşleşmiyorsa flag off.
        if (!string.IsNullOrWhiteSpace(flag.Segment))
        {
            var userSegment = await _segmentResolver.ResolveAsync(userId.Value, ct);
            if (!string.Equals(userSegment, flag.Segment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // 3) rollout (deterministic hash bucket)
        if (flag.RolloutPercentage <= 0)
            return false;
        if (flag.RolloutPercentage >= 100)
            return flag.IsEnabled;

        var bucket = StableBucket(userId.Value, key);
        if (bucket >= flag.RolloutPercentage)
            return false;

        // 4) global enabled
        return flag.IsEnabled;
    }

    private async Task<FeatureFlag?> GetFlagAsync(string key, CancellationToken ct)
    {
        var cacheKey = FlagCacheKeyPrefix + key;
        if (_cache.TryGetValue(cacheKey, out FeatureFlag? cached))
            return cached;

        var flag = await _db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Key == key, ct);
        if (flag is not null)
            _cache.Set(cacheKey, flag, FlagCacheTtl);
        return flag;
    }

    /// <summary>Stable 0-99 bucket. SHA256 ilk 4 byte -> uint -> mod 100.</summary>
    internal static int StableBucket(int userId, string key)
    {
        Span<byte> bytes = stackalloc byte[Encoding.UTF8.GetByteCount(key) + 4];
        BitConverter.TryWriteBytes(bytes, userId);
        Encoding.UTF8.GetBytes(key, bytes[4..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        var n = BitConverter.ToUInt32(hash[..4]);
        return (int)(n % 100u);
    }
}
