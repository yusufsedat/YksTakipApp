using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Api.Helpers; 

namespace YksTakipApp.Api.Endpoints
{
    public static class TopicEndpoints
    {
        public static void MapTopicEndpoints(this WebApplication app)
        {
            // Yeni konu ekleme (sadece Admin — global katalog)
            app.MapPost("/topics", [Authorize] async (
                TopicCreateRequest req,
                IValidator<TopicCreateRequest> validator,
                ITopicService service,
                HttpContext ctx) =>
            {
                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());
                await service.AddTopicAsync(req.Name, req.Category, req.Subject ?? "");
                return Results.Ok(new { message = "Konu eklendi." });
            })
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("writes");

            // Tüm konuları listeleme
            app.MapGet("/topics", async (ITopicService service, int page = 1, int pageSize = 20, string? sort = null) =>
            {
                page = page < 1 ? 1 : page;
                pageSize = Math.Clamp(pageSize, 1, 500);

                var topics = await service.GetAllAsync();
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
            });

            // Kullanıcının konu durumlarını al
            app.MapGet("/user/topics", [Authorize] async (ITopicService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var data = await service.GetUserTopicsAsync(userId.Value);
                return Results.Ok(data);
            });

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
            }).RequireRateLimiting("writes");

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
            }).RequireRateLimiting("writes");

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
            }).RequireRateLimiting("writes");
        }
    }
}
