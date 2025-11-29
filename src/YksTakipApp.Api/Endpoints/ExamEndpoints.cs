using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
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
                var userId = ctx.GetUserId(); // 🔹 güvenli claim alma
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(req);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());
                await service.AddExamAsync(userId.Value, req.ExamName, req.Date, req.NetTyt, req.NetAyt);
                return Results.Ok(new { message = "Deneme sonucu eklendi." });
            }).RequireRateLimiting("writes");

            app.MapGet("/exam/list", [Authorize] async (IExamService service, HttpContext ctx, int page = 1, int pageSize = 20, string? sort = null) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                page = page < 1 ? 1 : page;
                pageSize = Math.Clamp(pageSize, 1, 100);

                var exams = await service.GetUserExamsAsync(userId.Value);
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
                else
                {
                    query = query.OrderByDescending(e => e.Date);
                }

                var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return Results.Ok(new { items, meta = new { page, pageSize, total } });
            });

            app.MapDelete("/exam/delete/{id}", [Authorize] async (int id, IExamService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                await service.DeleteExamAsync(userId.Value, id);
                return Results.Ok(new { message = "Deneme sonucu silindi." });
            });
        }
    }
}
