using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using YksTakipApp.Api.DTOs;
using YksTakipApp.Api.Helpers;
using YksTakipApp.Core.Interfaces;

namespace YksTakipApp.Api.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this WebApplication app)
        {
            // Kullanıcı kayıt
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
                    user = new { user.Id, user.Name, user.Email, role = user.Role }
                });
            })
            .WithTags("Users")
            .WithSummary("Yeni kullanıcı kaydı")
            .WithDescription("Yeni bir kullanıcı hesabı oluşturur. Email benzersiz olmalıdır.");

            // Kullanıcı giriş
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

                var token = JwtHelper.GenerateToken(user, config, TimeSpan.FromHours(1));
                var refreshToken = CreateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(7);
                await userService.UpdateRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);
                return Results.Ok(new
                {
                    message = "Giriş başarılı!",
                    token,
                    refreshToken,
                    user = new { user.Id, user.Name, user.Email, role = user.Role }
                });
            })
            .RequireRateLimiting("login")
            .WithTags("Users")
            .WithSummary("Kullanıcı girişi")
            .WithDescription("Email ve şifre ile giriş yapar, access token ve refresh token döner.");

            async Task<IResult> RefreshTokenHandler(
                RefreshTokenRequest request,
                IUserService userService,
                IConfiguration config)
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                    return Results.BadRequest(new { message = "Refresh token zorunludur." });

                var user = await userService.GetByRefreshTokenAsync(request.RefreshToken);
                if (user is null || user.RefreshTokenExpiry is null || user.RefreshTokenExpiry <= DateTime.UtcNow)
                    return Results.Unauthorized();

                var newAccessToken = JwtHelper.GenerateToken(user, config, TimeSpan.FromHours(1));
                var newRefreshToken = CreateRefreshToken();
                var newRefreshExpiry = DateTime.UtcNow.AddDays(7);
                await userService.UpdateRefreshTokenAsync(user.Id, newRefreshToken, newRefreshExpiry);

                return Results.Ok(new
                {
                    token = newAccessToken,
                    refreshToken = newRefreshToken,
                    user = new { user.Id, user.Name, user.Email, role = user.Role }
                });
            }

            app.MapPost("/users/refresh-token", RefreshTokenHandler)
                .RequireRateLimiting("login")
                .WithTags("Users")
                .WithSummary("Access token yenile")
                .WithDescription("Geçerli bir refresh token ile yeni access token ve refresh token üretir.");

            app.MapPost("/refresh-token", RefreshTokenHandler)
                .RequireRateLimiting("login")
                .WithTags("Users")
                .WithSummary("Access token yenile (legacy)")
                .WithDescription("Geriye dönük uyumluluk için refresh token endpointi. /users/refresh-token ile aynı işlemi yapar.");

            // Kullanıcı profilini döndür (Authorize zorunlu)
            app.MapGet("/users/me", [Authorize] async (
                IUserService userService,
                ITopicService topicService,
                IStudyTimeService studyService,
                IExamService examService,
                IStatsService statsService,
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
                var examStreakDays = await statsService.GetExamStreakDaysAsync(userId.Value);

                var profile = new
                {
                    id = user.Id,
                    name = user.Name,
                    email = user.Email,
                    role = user.Role,
                    topics,
                    stats = new
                    {
                        totalMinutesLast7Days = totalMinutes,
                        examCount = exams.Count(),
                        examStreakDays
                    }
                };

                return Results.Ok(profile);
            })
            .WithTags("Users")
            .WithSummary("Profil bilgisi")
            .WithDescription("Giriş yapan kullanıcının profil, konu ve özet istatistik bilgilerini döndürür.");
        }

        private static string CreateRefreshToken()
        {
            Span<byte> bytes = stackalloc byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
