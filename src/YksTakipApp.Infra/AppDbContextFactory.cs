using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace YksTakipApp.Infra
{
    /// <summary>
    /// EF Core design-time factory. Migration komutları için kullanılır.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Design-time için connection string'i environment variable'dan veya appsettings'ten al
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? "Server=localhost;Database=YksTakipApp;User=root;Password=;Port=3306";

            var skipVersionDetect = string.Equals(
                Environment.GetEnvironmentVariable("MYSQL_SKIP_VERSION_DETECT"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var serverVersion = skipVersionDetect
                ? new MySqlServerVersion(new Version(8, 0, 36))
                : ServerVersion.AutoDetect(connectionString);

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(connectionString, serverVersion);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}

