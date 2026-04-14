using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Entities;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints
{
    public static class ProblemNoteEndpoints
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static void MapProblemNoteEndpoints(this WebApplication app)
        {
            app.MapGet("/problem-notes/list", [Authorize] async (IProblemNoteService service, HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var list = await service.ListAsync(userId.Value);
                var dtos = list.Select(ToDto).ToList();
                return Results.Ok(new { items = dtos });
            })
            .WithTags("ProblemNotes")
            .WithSummary("Soru notlarını listele")
            .WithDescription("Kullanıcının kaydettiği soru notlarını, etiket ve çözüm bilgileri ile listeler.");

            app.MapPost("/problem-notes/add", [Authorize] async (
                ProblemNoteCreateRequest request,
                IValidator<ProblemNoteCreateRequest> validator,
                IProblemNoteService service,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                var tags = request.Tags ?? new List<string>();
                var created = await service.AddAsync(userId.Value, request.ImageBase64, tags, request.SolutionLearned);
                return Results.Ok(ToDto(created));
            })
            .RequireRateLimiting("writes")
            .WithTags("ProblemNotes")
            .WithSummary("Soru notu ekle")
            .WithDescription("Yeni bir soru notu oluşturur; görsel (base64/url), etiket ve öğrenilen çözüm bilgisini kaydeder.");

            app.MapPut("/problem-notes/{id:int}", [Authorize] async (
                int id,
                ProblemNoteUpdateRequest request,
                IValidator<ProblemNoteUpdateRequest> validator,
                IProblemNoteService service,
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
                    var tags = request.Tags ?? new List<string>();
                    await service.UpdateAsync(userId.Value, id, tags, request.SolutionLearned, request.ImageBase64);
                    return Results.Ok(new { message = "Güncellendi." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("ProblemNotes")
            .WithSummary("Soru notu güncelle")
            .WithDescription("Belirtilen soru notunun etiket, çözüm notu ve görsel bilgisini günceller.");

            app.MapDelete("/problem-notes/{id:int}", [Authorize] async (int id, IProblemNoteService service, HttpContext ctx) =>
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
            .WithTags("ProblemNotes")
            .WithSummary("Soru notu sil")
            .WithDescription("Belirtilen soru notunu kullanıcının kayıtlarından kaldırır.");
        }

        private static ProblemNoteDto ToDto(ProblemNote e) => new()
        {
            Id = e.Id,
            ImageUrl = e.ImageUrl,
            Tags = DeserializeTags(e.TagsJson),
            SolutionLearned = e.SolutionLearned,
            CreatedAt = e.CreatedAt,
        };

        private static List<string> DeserializeTags(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
