using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Api.Helpers; 

namespace YksTakipApp.Api.Endpoints
{
    public static class TopicEndpoints
    {
        private const string TopicsCatalogCacheKey = "topics-catalog:v1";
        private static readonly TimeSpan TopicsCatalogCacheTtl = TimeSpan.FromHours(24);

        public static void MapTopicEndpoints(this WebApplication app)
        {
            // Yeni konu ekleme (sadece Admin — global katalog)
            app.MapPost("/topics", [Authorize] async (
                TopicCreateRequest req,
                IValidator<TopicCreateRequest> validator,
                ITopicService service,
                IMemoryCache cache,
                ILoggerFactory loggerFactory,
                HttpContext ctx) =>
            {
                var logger = loggerFactory.CreateLogger("TopicsCatalog");
                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());
                await service.AddTopicAsync(req.Name, req.Category, req.Subject ?? "");
                cache.Remove(TopicsCatalogCacheKey);
                logger.LogInformation(
                    "Topics catalog invalidated after mutation. CacheKey={CacheKey} Action={Action}",
                    TopicsCatalogCacheKey,
                    "AddTopic");
                return Results.Ok(new { message = "Konu eklendi." });
            })
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("writes")
                .WithTags("Topics")
                .WithSummary("Global konu ekle (Admin)")
                .WithDescription("Sadece Admin rolündeki kullanıcılar global konu kataloğuna yeni konu ekleyebilir.");

            // Tüm konuları listeleme
            app.MapGet("/topics", async (ITopicService service, IMemoryCache cache, ILoggerFactory loggerFactory, int page = 1, int pageSize = 20, string? sort = null) =>
            {
                var logger = loggerFactory.CreateLogger("TopicsCatalog");
                page = page < 1 ? 1 : page;
                pageSize = Math.Clamp(pageSize, 1, 500);

                YksTakipApp.Core.Entities.Topic[] topics;
                if (cache.TryGetValue(TopicsCatalogCacheKey, out YksTakipApp.Core.Entities.Topic[]? cached) && cached is not null)
                {
                    logger.LogDebug("Topics catalog cache hit. CacheKey={CacheKey}", TopicsCatalogCacheKey);
                    topics = cached;
                }
                else
                {
                    logger.LogDebug("Topics catalog cache miss. CacheKey={CacheKey}", TopicsCatalogCacheKey);
                    var loaded = await service.GetAllAsync();
                    topics = loaded.ToArray();
                    cache.Set(TopicsCatalogCacheKey, topics, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TopicsCatalogCacheTtl
                    });
                }
                var total = topics.Count();
                var query = topics.AsQueryable();

                if (!string.IsNullOrWhiteSpace(sort))
                {
                    query = sort.ToLower() switch
                    {
                        "name" => query.OrderBy(t => t.Name),
                        "-name" => query.OrderByDescending(t => t.Name),
                        "category" => query.OrderBy(t => t.Category),
                        "-category" => query.OrderByDescending(t => t.Category),
                        _ => query
                    };
                }

                var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return Results.Ok(new { items, meta = new { page, pageSize, total } });
            })
            .WithTags("Topics")
            .WithSummary("Konu kataloğunu listele")
            .WithDescription("Sistemdeki tüm konuları sayfalı olarak listeler. Opsiyonel sıralama destekler.");

            // Kullanıcının konu durumlarını al
            app.MapGet("/user/topics", [Authorize] async (ITopicService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var data = await service.GetUserTopicsAsync(userId.Value);
                return Results.Ok(data);
            })
            .WithTags("Topics")
            .WithSummary("Kullanıcının konularını getir")
            .WithDescription("Giriş yapan kullanıcının konu listesini ve ilerleme durumlarını döndürür.");

            // Kullanıcıya konu ekleme
            app.MapPost("/user/topics/add", [Authorize] async (
                UserTopicAddRequest req,
                IValidator<UserTopicAddRequest> validator,
                ITopicService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                try
                {
                    await service.AddUserTopicAsync(userId.Value, req.TopicId);
                    return Results.Ok(new { message = "Konu listenize eklendi." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("Topics")
            .WithSummary("Kullanıcıya konu ekle")
            .WithDescription("Global katalogdaki bir konuyu giriş yapan kullanıcının konu listesine ekler.");

            // Kullanıcının konu durumunu güncelle
            app.MapPost("/user/topics/update", [Authorize] async (
                UserTopicUpdateRequest req,
                IValidator<UserTopicUpdateRequest> validator,
                ITopicService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                try
                {
                    await service.UpdateUserTopicAsync(userId.Value, req.TopicId, req.Status);
                    return Results.Ok(new { message = "Durum güncellendi." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("Topics")
            .WithSummary("Konu durumunu güncelle")
            .WithDescription("Kullanıcının konu durumu bilgisini (ör. başlamadı/devam ediyor/bitti) günceller.");

            // Kullanıcı listesinden konu kaldır (UserTopic silinir; global Topic kalır)
            app.MapDelete("/user/topics/{topicId:int}", [Authorize] async (
                int topicId,
                ITopicService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await service.RemoveUserTopicAsync(userId.Value, topicId);
                    return Results.Ok(new { message = "Konu listenizden kaldırıldı." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("Topics")
            .WithSummary("Kullanıcı listesinden konu kaldır")
            .WithDescription("Belirtilen konuyu sadece kullanıcının kişisel listesinden kaldırır; global katalogdan silmez.");
        }
    }
}
