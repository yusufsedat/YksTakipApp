using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YksTakipApp.Application.Services;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Services;

public sealed class FeatureFlagServiceTests
{
    private sealed class FakeSegmentResolver : IUserSegmentResolver
    {
        private readonly Func<int, string?> _resolver;
        public FakeSegmentResolver(Func<int, string?> resolver) => _resolver = resolver;
        public Task<string?> ResolveAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult(_resolver(userId));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static FeatureFlagService Create(
        AppDbContext db,
        Func<int, string?>? segment = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new FeatureFlagService(db, cache, new FakeSegmentResolver(segment ?? (_ => null)));
    }

    [Fact]
    public async Task UnknownFlag_ReturnsFalse()
    {
        await using var db = CreateDb();
        var sut = Create(db);
        var enabled = await sut.IsEnabledAsync("missing.flag", userId: 1);
        enabled.Should().BeFalse();
    }

    [Fact]
    public async Task NullUserId_OnlyChecksGlobalIsEnabled()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k1", IsEnabled = true, RolloutPercentage = 0, Segment = "active_user" });
        db.FeatureFlags.Add(new FeatureFlag { Key = "k2", IsEnabled = false, RolloutPercentage = 100 });
        await db.SaveChangesAsync();

        var sut = Create(db);
        (await sut.IsEnabledAsync("k1", null)).Should().BeTrue();
        (await sut.IsEnabledAsync("k2", null)).Should().BeFalse();
    }

    [Fact]
    public async Task UserOverride_TakesPrecedenceOverEverything()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = false, RolloutPercentage = 0 });
        db.UserFeatureFlagOverrides.Add(new UserFeatureFlagOverride { UserId = 1, FlagKey = "k", IsEnabled = true });
        await db.SaveChangesAsync();

        var sut = Create(db);
        (await sut.IsEnabledAsync("k", 1)).Should().BeTrue();
    }

    [Fact]
    public async Task ExpiredOverride_IsIgnored()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = false, RolloutPercentage = 100 });
        db.UserFeatureFlagOverrides.Add(new UserFeatureFlagOverride
        {
            UserId = 1,
            FlagKey = "k",
            IsEnabled = true,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var sut = Create(db);
        (await sut.IsEnabledAsync("k", 1)).Should().BeFalse();
    }

    [Fact]
    public async Task SegmentMismatch_ReturnsFalse_RegardlessOfRollout()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = true, RolloutPercentage = 100, Segment = "beta" });
        await db.SaveChangesAsync();
        var sut = Create(db, _ => "active_user");
        (await sut.IsEnabledAsync("k", 1)).Should().BeFalse();
    }

    [Fact]
    public async Task SegmentMatch_RolloutFull_ReturnsTrue()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = true, RolloutPercentage = 100, Segment = "beta" });
        await db.SaveChangesAsync();
        var sut = Create(db, _ => "beta");
        (await sut.IsEnabledAsync("k", 1)).Should().BeTrue();
    }

    [Fact]
    public async Task RolloutZero_AllUsersFalse()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = true, RolloutPercentage = 0 });
        await db.SaveChangesAsync();

        var sut = Create(db);
        for (var i = 1; i <= 50; i++)
            (await sut.IsEnabledAsync("k", i)).Should().BeFalse();
    }

    [Fact]
    public async Task RolloutHundred_AllUsersTrue_WhenGlobalEnabled()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = true, RolloutPercentage = 100 });
        await db.SaveChangesAsync();

        var sut = Create(db);
        for (var i = 1; i <= 50; i++)
            (await sut.IsEnabledAsync("k", i)).Should().BeTrue();
    }

    [Fact]
    public async Task RolloutHash_DeterministicForSameUserKey()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = true, RolloutPercentage = 50 });
        await db.SaveChangesAsync();
        var sut = Create(db);

        var first = await sut.IsEnabledAsync("k", 42);
        for (var i = 0; i < 10; i++)
            (await sut.IsEnabledAsync("k", 42)).Should().Be(first);
    }

    [Fact]
    public void StableBucket_IsInRange0To99()
    {
        for (var u = 1; u <= 1000; u++)
        {
            var b = FeatureFlagService.StableBucket(u, "any.key");
            b.Should().BeInRange(0, 99);
        }
    }

    [Fact]
    public async Task GlobalDisabled_OverridesSegmentMatch_StillFalse()
    {
        await using var db = CreateDb();
        db.FeatureFlags.Add(new FeatureFlag { Key = "k", IsEnabled = false, RolloutPercentage = 100, Segment = "beta" });
        await db.SaveChangesAsync();
        var sut = Create(db, _ => "beta");
        (await sut.IsEnabledAsync("k", 1)).Should().BeFalse();
    }
}
