using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YksTakipApp.Infra;

namespace YksTakipApp.Tests.Integration;

// Top-level program için WebApplicationFactory
public class CustomWebApplicationFactory : WebApplicationFactory<YksTakipApp.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Production DbContext'i kaldır
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // DbContextPool'u kaldır (eğer varsa)
            var poolDescriptors = services.Where(
                d => d.ServiceType.IsGenericType && 
                     d.ServiceType.GetGenericTypeDefinition() == typeof(Microsoft.Extensions.ObjectPool.ObjectPool<>))
                .ToList();
            
            foreach (var poolDesc in poolDescriptors)
            {
                services.Remove(poolDesc);
            }

            // InMemory database ekle (sabit isim - test isolation için her test class'ı için ayrı factory kullanılabilir)
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });
        });

        builder.UseEnvironment("Testing");
        
        // Test için JWT key configuration
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", "test-secret-key-min-32-characters-long-for-testing-purposes-only" },
                { "Jwt:Issuer", "YksTakipApp" },
                { "Jwt:Audience", "YksTakipAppUsers" }
            });
        });
    }
}

