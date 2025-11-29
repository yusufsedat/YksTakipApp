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
                await service.AddStudyTimeAsync(userId.Value, request.DurationMinutes, request.Date);
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

                var times = (await service.GetStudyTimesAsync(userId.Value)).OrderByDescending(t => t.Date);
                var total = times.Count();
                var items = times.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return Results.Ok(new { items, meta = new { page, pageSize, total } });
            });
        }
    }
}
