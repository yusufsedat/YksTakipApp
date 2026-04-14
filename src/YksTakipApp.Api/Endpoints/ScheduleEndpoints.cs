using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints
{
    public static class ScheduleEndpoints
    {
        public static void MapScheduleEndpoints(this WebApplication app)
        {
            app.MapGet("/schedule/list", [Authorize] async (IScheduleService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var list = await service.GetListAsync(userId.Value);
                var dtos = list.Select(ToDto).ToList();
                return Results.Ok(new { items = dtos });
            })
            .WithTags("Schedule")
            .WithSummary("Program listesini getir")
            .WithDescription("Giriş yapan kullanıcının haftalık/aylık program kayıtlarını listeler.");

            app.MapPost("/schedule/add", [Authorize] async (
                ScheduleCreateRequest request,
                IValidator<ScheduleCreateRequest> validator,
                IScheduleService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                try
                {
                    var created = await service.AddAsync(
                        userId.Value,
                        request.Recurrence,
                        request.DayOfWeek,
                        request.DayOfMonth,
                        request.StartMinute,
                        request.EndMinute,
                        request.Title,
                        request.TopicId);
                    return Results.Ok(ToDto(created));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("Schedule")
            .WithSummary("Programa kayıt ekle")
            .WithDescription("Kullanıcı programına yeni bir zaman aralığı ve isteğe bağlı konu bağlantısı ekler.");

            app.MapPut("/schedule/{id:int}", [Authorize] async (
                int id,
                ScheduleUpdateRequest request,
                IValidator<ScheduleUpdateRequest> validator,
                IScheduleService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                try
                {
                    await service.UpdateAsync(
                        userId.Value,
                        id,
                        request.Recurrence,
                        request.DayOfWeek,
                        request.DayOfMonth,
                        request.StartMinute,
                        request.EndMinute,
                        request.Title,
                        request.TopicId);
                    return Results.Ok(new { message = "Güncellendi." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("Schedule")
            .WithSummary("Program kaydını güncelle")
            .WithDescription("Belirtilen program kaydının zaman, tekrar tipi, başlık ve konu bilgisini günceller.");

            app.MapDelete("/schedule/{id:int}", [Authorize] async (int id, IScheduleService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                try
                {
                    await service.DeleteAsync(userId.Value, id);
                    return Results.Ok(new { message = "Silindi." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("Schedule")
            .WithSummary("Program kaydını sil")
            .WithDescription("Belirtilen program kaydını giriş yapan kullanıcının takviminden kaldırır.");
        }

        private static ScheduleEntryDto ToDto(ScheduleEntry e) => new()
        {
            Id = e.Id,
            Recurrence = e.Recurrence,
            DayOfWeek = e.DayOfWeek,
            DayOfMonth = e.DayOfMonth,
            StartMinute = e.StartMinute,
            EndMinute = e.EndMinute,
            Title = e.Title,
            TopicId = e.TopicId,
        };
    }
}
