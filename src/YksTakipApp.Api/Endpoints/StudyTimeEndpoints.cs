using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Core.Interfaces;
using YksTakipApp.Api.Helpers; 
using YksTakipApp.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
                HttpContext ctx,
                ILoggerFactory loggerFactory) =>
            {
                var userId = ctx.GetUserId(); // güvenli claim alma
                if (userId is null)
                    return Results.Unauthorized();
                var idempotencyKey = RequestContextHelper.ResolveIdempotencyKey(ctx, request.ClientRequestId);
                var log = loggerFactory.CreateLogger("StudyTimeEndpoints");
                using var _ = RequestContextHelper.PushOperationContext(ctx, "StudyTime.Add", userId, idempotencyKey);

                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());
                try
                {
                    var result = await service.CreateOrAccumulateStudyTimeIdempotentAsync(
                        userId.Value, request.DurationMinutes, request.Date, request.TopicId, idempotencyKey);
                    log.LogInformation("StudyTime add finished with {Result}.", result.IsReplay ? "replay" : "success");
                }
                catch (InvalidOperationException ex)
                {
                    log.LogWarning(ex, "StudyTime add validation failed with {Result}.", "fail");
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
                HttpContext ctx,
                ILoggerFactory loggerFactory) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();
                var idempotencyKey = RequestContextHelper.ResolveIdempotencyKey(ctx, request.ClientRequestId);
                var log = loggerFactory.CreateLogger("StudyTimeEndpoints");
                using var _ = RequestContextHelper.PushOperationContext(ctx, "StudyTime.Create", userId, idempotencyKey);

                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                try
                {
                    var created = await service.CreateOrAccumulateStudyTimeIdempotentAsync(
                        userId.Value, request.DurationMinutes, request.Date, request.TopicId, idempotencyKey);
                    var saved = created.Entity;
                    if (created.IsReplay)
                        log.LogWarning("StudyTime create replayed with {Result}.", "replay");
                    else
                        log.LogInformation("StudyTime create finished with {Result}.", "success");
                    return Results.Ok(new
                    {
                        message = "Çalışma süresi kaydedildi.",
                        replay = created.IsReplay,
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
                    log.LogWarning(ex, "StudyTime create failed with {Result}.", "fail");
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireRateLimiting("writes")
            .WithTags("StudyTime")
            .WithSummary("Kronometre çalışma kaydı oluştur")
            .WithDescription("Kronometre akışı için optimize edilmiş çalışma kaydı endpointidir; aynı gün/konu kayıtlarını birleştirir.");

            app.MapPost("/studytime/bulk-create", [Authorize] async (
                StudyTimeBulkCreateRequest request,
                IValidator<StudyTimeRequest> validator,
                IStudyTimeService service,
                HttpContext ctx,
                ILoggerFactory loggerFactory) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();
                var log = loggerFactory.CreateLogger("StudyTimeEndpoints");
                using var _ = RequestContextHelper.PushOperationContext(ctx, "StudyTime.BulkCreate", userId, idempotencyKey: null);

                if (request.Items is null || request.Items.Count == 0)
                    return Results.BadRequest(new { message = "En az bir kayıt gönderilmelidir." });

                if (request.Items.Count > 200)
                    return Results.BadRequest(new { message = "Tek seferde en fazla 200 kayıt gönderilebilir." });

                var savedCount = 0;
                var failedIndexes = new List<int>();

                for (var i = 0; i < request.Items.Count; i++)
                {
                    var item = request.Items[i];
                    var validation = await validator.ValidateAsync(item);
                    if (!validation.IsValid)
                    {
                        failedIndexes.Add(i);
                        continue;
                    }

                    try
                    {
                        var result = await service.CreateOrAccumulateStudyTimeIdempotentAsync(
                            userId.Value, item.DurationMinutes, item.Date, item.TopicId, item.ClientRequestId);
                        savedCount++;
                        if (result.IsReplay)
                            log.LogWarning("Bulk studytime replayed for item index {ItemIndex}.", i);
                    }
                    catch (InvalidOperationException)
                    {
                        failedIndexes.Add(i);
                    }
                }

                log.LogInformation("StudyTime bulk create finished with {Result}. Saved={SavedCount}, Failed={FailedCount}.", "success", savedCount, failedIndexes.Count);

                return Results.Ok(new
                {
                    message = "Toplu çalışma kayıtları işlendi.",
                    savedCount,
                    failedCount = failedIndexes.Count,
                    failedIndexes
                });
            })
            .RequireRateLimiting("writes")
            .WithTags("StudyTime")
            .WithSummary("Toplu kronometre çalışma kaydı oluştur")
            .WithDescription("Çevrimdışı kuyrukta biriken çalışma kayıtlarını tek istekte gönderir ve aynı gün/konu kayıtlarını birleştirir.");

            // Mobil uyum endpoint: POST /api/studytimes (UserId, DurationMinutes, Subject, Date)
            app.MapPost("/api/studytimes", [Authorize] async (
                StudyTimeCreateApiRequest request,
                IStudyTimeService service,
                AppDbContext db,
                HttpContext ctx,
                ILoggerFactory loggerFactory) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();
                var idempotencyKey = RequestContextHelper.ResolveIdempotencyKey(ctx, request.ClientRequestId);
                var log = loggerFactory.CreateLogger("StudyTimeEndpoints");
                using var _ = RequestContextHelper.PushOperationContext(ctx, "StudyTime.CreateApi", userId, idempotencyKey);

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
                    var created = await service.CreateOrAccumulateStudyTimeIdempotentAsync(
                        userId.Value,
                        request.DurationMinutes,
                        request.Date,
                        topicId,
                        idempotencyKey);
                    var saved = created.Entity;
                    log.LogInformation("StudyTime api create finished with {Result}.", created.IsReplay ? "replay" : "success");

                    return Results.Ok(new
                    {
                        message = "Çalışmalarım bölümüne eklendi!",
                        replay = created.IsReplay,
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
                    log.LogWarning(ex, "StudyTime api create failed with {Result}.", "fail");
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
