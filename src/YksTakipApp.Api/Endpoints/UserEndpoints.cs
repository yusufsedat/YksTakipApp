using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {
            // 🔹 Kullanıcı kayıt
            app.MapPost("/users/register", async (
                RegisterRequest request,
                IValidator<RegisterRequest> validator,
                IUserService userService,
                HttpContext ctx) =>
            {
                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                var existing = await userService.GetByEmailAsync(request.Email);
                if (existing is not null)
                    return Results.BadRequest(new { message = "Bu email zaten kayıtlı." });

                var user = await userService.RegisterAsync(request.Name, request.Email, request.Password);
                return Results.Ok(new
                {
                    message = "Kayıt başarılı!",
                    user = new { user.Id, user.Name, user.Email }
                });
            });

            // 🔹 Kullanıcı giriş
            app.MapPost("/users/login", async (
                LoginRequest request,
                IValidator<LoginRequest> validator,
                IUserService userService,
                IConfiguration config,
                HttpContext ctx) =>
            {
                var validation = await validator.ValidateAsync(request);
                if (!validation.IsValid)
                    return ctx.ValidationProblem(validation.ToDictionary());

                var user = await userService.GetByEmailAsync(request.Email);
                if (user is null)
                    return Results.BadRequest(new { message = "Email bulunamadı." });

                if (!userService.VerifyPassword(request.Password, user.PasswordHash))
                    return Results.BadRequest(new { message = "Şifre hatalı." });

                var token = JwtHelper.GenerateToken(user, config);
                return Results.Ok(new
                {
                    message = "Giriş başarılı!",
                    token,
                    user = new { user.Id, user.Name, user.Email }
                });
            }).RequireRateLimiting("login");

            // 🔹 Kullanıcı profilini döndür (Authorize zorunlu)
            app.MapGet("/users/me", [Authorize] async (
                IUserService userService,
                ITopicService topicService,
                IStudyTimeService studyService,
                IExamService examService,
                HttpContext ctx) =>
            {
                var userId = ctx.GetUserId();
                if (userId is null)
                    return Results.Unauthorized();

                var user = await userService.GetByIdAsync(userId.Value);
                if (user is null)
                    return Results.NotFound(new { message = "Kullanıcı bulunamadı." });

                var topics = await topicService.GetUserTopicsAsync(userId.Value);
                var totalMinutes = await studyService.GetTotalMinutesLast7DaysAsync(userId.Value);
                var exams = await examService.GetUserExamsAsync(userId.Value);

                var profile = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    topics,
                    stats = new
                    {
                        totalMinutesLast7Days = totalMinutes,
                        examCount = exams.Count()
                    }
                };

                return Results.Ok(profile);
            });
        }
    }
}
