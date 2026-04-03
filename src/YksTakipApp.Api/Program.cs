using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using Amazon.Lambda.AspNetCoreServer.Hosting;

using YksTakipApp.Core.Interfaces;
using YksTakipApp.Application.Services;
using YksTakipApp.Infra;
using YksTakipApp.Infra.Repositories;
using YksTakipApp.Api.Endpoints;
using YksTakipApp.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// AWS Secrets Manager'dan konfig değerlerini yükle
var secretName = Environment.GetEnvironmentVariable("AWS__SECRETS_NAME");
var secrets = await YksTakipApp.Api.Helpers.SecretsLoader.TryLoadFromAwsAsync(secretName);
if (secrets is not null)
{
    foreach (var kv in secrets)
    {
        builder.Configuration[kv.Key] = kv.Value;
    }
}

// -----------------------------------------------------------------------------
// ✅ AWS Lambda Hosting (Sadece Lambda'da aktif olur)
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
// -----------------------------------------------------------------------------

// 💾 Database (MySQL + EF Core)
builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    
    var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                 ?? builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connStr))
        throw new InvalidOperationException("Database connection string missing. Configure 'ConnectionStrings:DefaultConnection' via environment.");

    var usingRdsProxy = connStr.Contains("proxy-", StringComparison.OrdinalIgnoreCase) ||
                        connStr.Contains("rds.amazonaws.com", StringComparison.OrdinalIgnoreCase);

    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr), mySqlOptions =>
    {
        mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });
});
builder.Services.AddScoped<DbContext, AppDbContext>();

// 🌐 CORS (React Native / Web istemcileri için)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: Tüm origin'lere izin ver
            policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .WithHeaders("Authorization", "Content-Type");
        }
        else
        {
            // Production: Sadece belirli origin'lere izin ver
            var allowedOrigins = Environment.GetEnvironmentVariable("CORS__AllowedOrigins")?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? new[] { "https://yourdomain.com" }; // Varsayılan origin'leri buraya ekle
            
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .WithHeaders("Authorization", "Content-Type")
                .AllowCredentials(); // Cookie/credential gönderimi için
        }
    });
});

// 🔐 JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = Environment.GetEnvironmentVariable("Jwt__Key") 
             ?? builder.Configuration["Jwt:Key"] 
             ?? builder.Configuration["Jwt__Key"] // WebApplicationFactory UseSetting formatı
             ?? jwtSettings["Key"]
             ?? (builder.Environment.IsEnvironment("Testing") 
                 ? "test-secret-key-min-32-characters-long-for-testing-purposes-only" 
                 : null);
var jwtIssuer = Environment.GetEnvironmentVariable("Jwt__Issuer") 
                ?? builder.Configuration["Jwt:Issuer"] 
                ?? builder.Configuration["Jwt__Issuer"]
                ?? jwtSettings["Issuer"]
                ?? (builder.Environment.IsEnvironment("Testing") ? "YksTakipApp" : null);
var jwtAudience = Environment.GetEnvironmentVariable("Jwt__Audience") 
                  ?? builder.Configuration["Jwt:Audience"] 
                  ?? builder.Configuration["Jwt__Audience"]
                  ?? jwtSettings["Audience"]
                  ?? (builder.Environment.IsEnvironment("Testing") ? "YksTakipAppUsers" : null);

// Production guard: zayıf veya eksik anahtar/issuer/audience engelle
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == "super-secret-key-change-this-later")
        throw new InvalidOperationException("JWT key is not securely configured in production. Provide Jwt__Key via environment/secret store.");
    
    // JWT key minimum uzunluk kontrolü (güvenlik için en az 32 karakter)
    if (jwtKey!.Length < 32)
        throw new InvalidOperationException("JWT key must be at least 32 characters long in production.");
    
    if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
        throw new InvalidOperationException("JWT issuer/audience must be configured in production. Provide Jwt__Issuer and Jwt__Audience.");
}

// JWT key null kontrolü (test ortamı için)
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        jwtKey = "test-secret-key-min-32-characters-long-for-testing-purposes-only";
        jwtIssuer ??= "YksTakipApp";
        jwtAudience ??= "YksTakipAppUsers";
    }
    else
    {
        throw new InvalidOperationException("JWT key is required. Configure Jwt__Key via environment or configuration.");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("User", "Admin"));
});

// 🚦 Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;           // 5 istek
        opt.Window = TimeSpan.FromMinutes(1); // 1 dakikada
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("writes", opt =>
    {
        opt.PermitLimit = 60;          // dakika başına 60 yazma
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// 🧩 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "YksTakipApp API",
        Version = "v1",
        Description = "YKS Takip uygulaması için backend API dokümantasyonu"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT tokeninizi buraya giriniz. Örnek: **Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6...**"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 📜 HTTP Logging servisi (UseHttpLogging middleware için gerekli)
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.RequestPropertiesAndHeaders |
                            Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.ResponsePropertiesAndHeaders;
});

// 🧱 Dependency Injection (Repository + Services)
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IStudyTimeService, StudyTimeService>();
builder.Services.AddScoped<ITopicService, TopicService>();
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IProblemNoteService, ProblemNoteService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// 🌍 Pipeline Configuration
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Sadece geliştirme ortamında DB bağlantı testi endpoint'i
    app.MapGet("/dbtest", async (AppDbContext db) =>
    {
        return await db.Database.CanConnectAsync() ? "✅ Connected" : "❌ Not Connected";
    });
}

// 🔒 Security Headers (Production'da)
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
        
        // HSTS (HTTPS Strict Transport Security) - Lambda'da API Gateway HTTPS kullanır
        if (context.Request.IsHttps)
        {
            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }
        
        await next();
    });
}

app.UseCors("DefaultCors");

// 🧯 Global Exception Handling
app.UseMiddleware<YksTakipApp.Api.Helpers.GlobalExceptionMiddleware>();

// 📜 HTTP request logging (CloudWatch uyumlu)
// Test ortamında HttpLogging'i devre dışı bırak (ObjectPool sorunu nedeniyle)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpLogging();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// 📚 Endpoints
app.MapGet("/", () => "✅ YksTakipApp Lambda API running!");

app.MapUserEndpoints();
app.MapTopicEndpoints();
app.MapExamEndpoints();
app.MapStudyTimeEndpoints();
app.MapStatsEndpoints();
app.MapScheduleEndpoints();
app.MapProblemNoteEndpoints();

// 🌱 Development: demo kullanıcı ve örnek veri (demo@ykstakip.local yoksa bir kez eklenir)
if (app.Environment.IsDevelopment()
    && !string.Equals(Environment.GetEnvironmentVariable("SKIP_DEV_SEED"), "true", StringComparison.OrdinalIgnoreCase))
{
    using var seedScope = app.Services.CreateScope();
    var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Dev ortamında şema güncel değilse (ör. yeni migration eklendiyse) seed sırasında patlamamak için
    // migration'ları otomatik uygula.
    await db.Database.MigrateAsync();
    var appConfig = seedScope.ServiceProvider.GetRequiredService<IConfiguration>();
    var yksLogger = seedScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("YksCurriculumSeed");
    await YksCurriculumSeed.EnsureAsync(db, yksLogger, appConfig);
    var seedLogger = seedScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DevDataSeeder");
    await DevDataSeeder.SeedAsync(db, seedLogger);
    var adminLogger = seedScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminDevSeeder");
    await AdminDevSeeder.EnsureAsync(db, adminLogger);
}

// 🚀 Run
// Lambda'da AddAWSLambdaHosting otomatik olarak app.Run()'u handle eder
// Local development'ta normal şekilde çalışır
app.Run();
