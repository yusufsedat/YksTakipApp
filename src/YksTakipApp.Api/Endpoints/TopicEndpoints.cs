using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Api.Helpers; // 🔹 GetUserId() uzantısı için

namespace YksTakipApp.Api.Endpoints
{
    public static class TopicEndpoints
    {
        public static void MapTopicEndpoints(this WebApplication app)
        {
            // 🔹 Yeni konu ekleme (admin veya system kullanımı)
            app.MapPost("/topics", [Authorize] async (
                TopicCreateRequest req,
                IValidator<TopicCreateRequest> validator,
                ITopicService service,
                HttpContext ctx) =>
            {
                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());
                await service.AddTopicAsync(req.Name, req.Category);
                return Results.Ok(new { message = "Konu eklendi." });
            }).RequireRateLimiting("writes");

            // 🔹 Tüm konuları listeleme
            app.MapGet("/topics", async (ITopicService service, int page = 1, int pageSize = 20, string? sort = null) =>
            {
                page = page < 1 ? 1 : page;
                pageSize = Math.Clamp(pageSize, 1, 100);

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

            // 🔹 Kullanıcının konu durumlarını alma
            app.MapGet("/user/topics", [Authorize] async (ITopicService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId(); // güvenli claim alma
                if (userId is null)
                    return Results.Unauthorized();

                var data = await service.GetUserTopicsAsync(userId.Value);
                return Results.Ok(data);
            });

            // 🔹 Kullanıcıya konu ekleme (yeni konuyu kendi listesine ekler)
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

            // 🔹 Kullanıcının konu durumunu güncelleme (sadece mevcut konular için)
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
        }
    }
}
