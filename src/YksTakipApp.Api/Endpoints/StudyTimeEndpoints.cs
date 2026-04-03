using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Api.Helpers; 

namespace YksTakipApp.Api.Endpoints
{
    public static class StudyTimeEndpoints
    {
        public static void MapStudyTimeEndpoints(this WebApplication app)
        {
            // Çalışma süresi ekleme
            app.MapPost("/studytime/add", [Authorize] async (
                StudyTimeRequest request,
                IValidator<StudyTimeRequest> validator,
                IStudyTimeService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId(); // güvenli claim alma
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());
                try
                {
                    await service.AddStudyTimeAsync(userId.Value, request.DurationMinutes, request.Date, request.TopicId);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                return Results.Ok(new { message = "Çalışma süresi kaydedildi." });
            }).RequireRateLimiting("writes");

            // Kullanıcının çalışma sürelerini listeleme
            app.MapGet("/studytime/list", [Authorize] async (IStudyTimeService service, HttpContext ctx, int page = 1, int pageSize = 20) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                page = page < 1 ? 1 : page;
                pageSize = Math.Clamp(pageSize, 1, 100);

                var times = (await service.GetStudyTimesAsync(userId.Value)).OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
                var total = times.Count();
                var pageItems = times.Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(t => new
                    {
                        t.Id,
                        t.UserId,
                        date = t.Date,
                        t.DurationMinutes,
                        topicId = t.TopicId,
                        topicName = t.Topic != null ? t.Topic.Name : null
                    })
                    .ToList();
                return Results.Ok(new { items = pageItems, meta = new { page, pageSize, total } });
            });
        }
    }
}
