using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Api.Helpers; 
using YksTakipApp.Infra;
using Microsoft.EntityFrameworkCore;

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
                    await service.CreateOrAccumulateStudyTimeAsync(userId.Value, request.DurationMinutes, request.Date, request.TopicId);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                return Results.Ok(new { message = "Çalışma süresi kaydedildi." });
            })
            .RequireRateLimiting("writes")
            .WithTags("StudyTime")
            .WithSummary("Çalışma süresi ekle")
            .WithDescription("Kullanıcının belirttiği tarih ve konu için çalışma süresi kaydı oluşturur veya mevcut kaydı biriktirir.");

            // Kronometre bitim kaydı için optimize create endpoint (aynı gün+konu kayıtlarını birleştirir)
            app.MapPost("/studytime/create", [Authorize] async (
                StudyTimeRequest request,
                IValidator<StudyTimeRequest> validator,
                IStudyTimeService service,
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
                    var saved = await service.CreateOrAccumulateStudyTimeAsync(userId.Value, request.DurationMinutes, request.Date, request.TopicId);
                    return Results.Ok(new
                    {
                        message = "Çalışma süresi kaydedildi.",
                        item = new
                        {
                            saved.Id,
                            saved.UserId,
                            saved.Date,
                            saved.DurationMinutes,
                            saved.TopicId
                        }
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("StudyTime")
            .WithSummary("Kronometre çalışma kaydı oluştur")
            .WithDescription("Kronometre akışı için optimize edilmiş çalışma kaydı endpointidir; aynı gün/konu kayıtlarını birleştirir.");

            // Mobil uyum endpoint: POST /api/studytimes (UserId, DurationMinutes, Subject, Date)
            app.MapPost("/api/studytimes", [Authorize] async (
                StudyTimeCreateApiRequest request,
                IStudyTimeService service,
                AppDbContext db,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                if (request.UserId.HasValue && request.UserId.Value != userId.Value)
                    return Results.BadRequest(new { message = "UserId geçersiz." });

                if (request.DurationMinutes < 1 || request.DurationMinutes > 1440)
                    return Results.BadRequest(new { message = "DurationMinutes 1-1440 arasında olmalıdır." });

                if (request.Date == default)
                    return Results.BadRequest(new { message = "Date zorunludur." });

                int? topicId = null;
                if (!string.IsNullOrWhiteSpace(request.Subject))
                {
                    var normalized = request.Subject.Trim();
                    topicId = await db.UserTopics
                        .AsNoTracking()
                        .Where(ut => ut.UserId == userId.Value)
                        .Join(
                            db.Topics.AsNoTracking(),
                            ut => ut.TopicId,
                            t => t.Id,
                            (ut, t) => new { t.Id, t.Subject })
                        .Where(x => x.Subject == normalized)
                        .Select(x => (int?)x.Id)
                        .FirstOrDefaultAsync();
                }

                try
                {
                    var saved = await service.CreateOrAccumulateStudyTimeAsync(
                        userId.Value,
                        request.DurationMinutes,
                        request.Date,
                        topicId);

                    return Results.Ok(new
                    {
                        message = "Çalışmalarım bölümüne eklendi!",
                        item = new
                        {
                            saved.Id,
                            saved.UserId,
                            saved.Date,
                            saved.DurationMinutes,
                            saved.TopicId,
                            subject = request.Subject
                        }
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("StudyTime")
            .WithSummary("Mobil uyumlu çalışma kaydı")
            .WithDescription("Eski mobil istemci uyumluluğu için çalışma süresi ekler. Subject bilgisine göre kullanıcının konu eşlemesini yapar.");

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
            })
            .WithTags("StudyTime")
            .WithSummary("Çalışma sürelerini listele")
            .WithDescription("Giriş yapan kullanıcının çalışma sürelerini en yeniden eskiye sayfalı olarak listeler.");
        }
    }
}
