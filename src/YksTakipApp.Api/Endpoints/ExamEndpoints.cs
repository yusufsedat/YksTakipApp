using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;
using System.Security.Claims;
using YksTakipApp.Api.Helpers;

namespace YksTakipApp.Api.Endpoints
{
    public static class ExamEndpoints
    {
        public static void MapExamEndpoints(this WebApplication app)
        {
            app.MapPost("/exam/add", [Authorize] async (
                ExamResultRequest req,
                IValidator<ExamResultRequest> validator,
                IExamService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                var details = req.Details.Select(d => new ExamDetail
                {
                    Subject = d.Subject,
                    Correct = d.Correct,
                    Wrong = d.Wrong,
                    Blank = d.Blank
                });

                await service.AddExamAsync(
                    userId.Value, req.ExamName, req.Date, req.NetTyt, req.NetAyt,
                    req.ExamType, req.Subject, req.DurationMinutes, req.Difficulty,
                    req.ErrorReasons, details);

                return Results.Ok(new { message = "Deneme sonucu eklendi." });
            })
            .RequireRateLimiting("writes")
            .WithTags("Exams")
            .WithSummary("Deneme sonucu ekle")
            .WithDescription("Giriş yapan kullanıcı için TYT/AYT/branş deneme sonucu ve ders detaylarını kaydeder.");

            app.MapGet("/exam/list", [Authorize] async (
                IExamService service, HttpContext ctx,
                int page = 1, int pageSize = 20, string? sort = null, string? type = null) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                page = page < 1 ? 1 : page;
                pageSize = Math.Clamp(pageSize, 1, 100);

                var exams = await service.GetUserExamsAsync(userId.Value, type);
                var total = exams.Count();

                var query = exams.AsQueryable();
                if (!string.IsNullOrWhiteSpace(sort))
                {
                    query = sort.ToLower() switch
                    {
                        "date" => query.OrderBy(e => e.Date),
                        "-date" => query.OrderByDescending(e => e.Date),
                        "name" => query.OrderBy(e => e.ExamName),
                        "-name" => query.OrderByDescending(e => e.ExamName),
                        _ => query
                    };
                }

                var items = query.Skip((page - 1) * pageSize).Take(pageSize).Select(e => new
                {
                    e.Id,
                    e.ExamName,
                    e.ExamType,
                    e.Subject,
                    e.Date,
                    e.NetTyt,
                    e.NetAyt,
                    e.DurationMinutes,
                    e.Difficulty,
                    e.ErrorReasons,
                    Details = e.ExamDetails.Select(d => new
                    {
                        d.Id,
                        d.Subject,
                        d.Correct,
                        d.Wrong,
                        d.Blank,
                        d.Net
                    })
                }).ToList();

                return Results.Ok(new { items, meta = new { page, pageSize, total } });
            })
            .WithTags("Exams")
            .WithSummary("Deneme sonuçlarını listele")
            .WithDescription("Kullanıcının deneme geçmişini filtreleme, sıralama ve sayfalama ile döndürür.");

            app.MapDelete("/exam/delete/{id}", [Authorize] async (int id, IExamService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                await service.DeleteExamAsync(userId.Value, id);
                return Results.Ok(new { message = "Deneme sonucu silindi." });
            })
            .WithTags("Exams")
            .WithSummary("Deneme sonucu sil")
            .WithDescription("Belirtilen deneme kaydını giriş yapan kullanıcı adına siler.");
        }
    }
}
