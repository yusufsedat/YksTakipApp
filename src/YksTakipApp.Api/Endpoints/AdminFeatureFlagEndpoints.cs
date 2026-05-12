using Microsoft.EntityFrameworkCore;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Entities;
using YksTakipApp.Infra;

namespace YksTakipApp.Api.Endpoints;

public static class AdminFeatureFlagEndpoints
{
    private const int MaxTake = 100;
    private const int MaxSkip = 10000;

    private static readonly HashSet<string> SegmentWhitelist = new(StringComparer.OrdinalIgnoreCase)
    {
        "new_user",
        "active_user",
        "beta"
    };

    public static void MapAdminFeatureFlagEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/feature-flags")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin/FeatureFlags");

        group.MapGet("", async Task<IResult> (
            int? take,
            int? skip,
            AppDbContext db,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var t = take ?? 50;
            var s = skip ?? 0;
            if (t < 1 || t > MaxTake)
                return ctx.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["take"] = new[] { $"take must be in [1, {MaxTake}]." }
                });
            if (s < 0 || s > MaxSkip)
                return ctx.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["skip"] = new[] { $"skip must be in [0, {MaxSkip}]." }
                });

            var rows = await db.FeatureFlags
                .AsNoTracking()
                .OrderBy(x => x.Key)
                .Skip(s).Take(t)
                .Select(x => new FeatureFlagDto
                {
                    Key = x.Key,
                    IsEnabled = x.IsEnabled,
                    Description = x.Description,
                    RolloutPercentage = x.RolloutPercentage,
                    Segment = x.Segment,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(ct);
            return Results.Ok(rows);
        })
        .WithSummary("Feature flag listesi (sayfalı)");

        group.MapPut("/{key}", async Task<IResult> (
            string key,
            UpdateFeatureFlagRequest body,
            AppDbContext db,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body.Segment is not null
                && body.Segment.Length > 0
                && !SegmentWhitelist.Contains(body.Segment))
                return ctx.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["segment"] = new[] { "Segment must be one of: new_user, active_user, beta." }
                });
            if (body.RolloutPercentage is not null
                && (body.RolloutPercentage < 0 || body.RolloutPercentage > 100))
                return ctx.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["rolloutPercentage"] = new[] { "rolloutPercentage must be in [0, 100]." }
                });

            var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, ct);
            if (flag is null)
                return Results.NotFound();

            if (body.IsEnabled is not null) flag.IsEnabled = body.IsEnabled.Value;
            if (body.RolloutPercentage is not null) flag.RolloutPercentage = body.RolloutPercentage.Value;
            if (body.Segment is not null) flag.Segment = string.IsNullOrWhiteSpace(body.Segment) ? null : body.Segment;
            if (body.Description is not null) flag.Description = body.Description;
            flag.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new FeatureFlagDto
            {
                Key = flag.Key,
                IsEnabled = flag.IsEnabled,
                Description = flag.Description,
                RolloutPercentage = flag.RolloutPercentage,
                Segment = flag.Segment,
                CreatedAt = flag.CreatedAt,
                UpdatedAt = flag.UpdatedAt
            });
        })
        .WithSummary("Feature flag güncelle (segment whitelist + rollout clamp)");

        group.MapPost("/{key}/overrides", async Task<IResult> (
            string key,
            UpsertFlagOverrideRequest body,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var flag = await db.FeatureFlags.AsNoTracking().AnyAsync(f => f.Key == key, ct);
            if (!flag)
                return Results.NotFound();

            var existing = await db.UserFeatureFlagOverrides
                .FirstOrDefaultAsync(o => o.UserId == body.UserId && o.FlagKey == key, ct);
            if (existing is null)
            {
                db.UserFeatureFlagOverrides.Add(new UserFeatureFlagOverride
                {
                    UserId = body.UserId,
                    FlagKey = key,
                    IsEnabled = body.IsEnabled,
                    ExpiresAt = body.ExpiresAt,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.IsEnabled = body.IsEnabled;
                existing.ExpiresAt = body.ExpiresAt;
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithSummary("Kullanıcıya flag override ekle/güncelle");

        group.MapDelete("/{key}/overrides/{userId:int}", async Task<IResult> (
            string key,
            int userId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var existing = await db.UserFeatureFlagOverrides
                .FirstOrDefaultAsync(o => o.UserId == userId && o.FlagKey == key, ct);
            if (existing is null)
                return Results.NotFound();
            db.UserFeatureFlagOverrides.Remove(existing);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithSummary("Kullanıcı flag override sil");
    }
}
