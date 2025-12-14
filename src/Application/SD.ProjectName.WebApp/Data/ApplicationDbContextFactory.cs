using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Data.Common;

namespace SD.ProjectName.WebApp.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            }

            var dataSource = GetDataSourceFromConnectionString(connectionString);
            var useSqlite = !OperatingSystem.IsWindows() && IsLocalDbDataSource(dataSource);

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            if (useSqlite)
            {
                var sqliteConnectionString = configuration.GetConnectionString("SqliteConnection");
                if (string.IsNullOrWhiteSpace(sqliteConnectionString))
                {
                    throw new InvalidOperationException("Connection string 'SqliteConnection' not found.");
                }

                optionsBuilder.UseSqlite(sqliteConnectionString);
            }
            else
            {
                optionsBuilder.UseSqlServer(connectionString);
            }

            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private static string? GetDataSourceFromConnectionString(string connectionString)
        {
            var sqlConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (sqlConnectionStringBuilder.TryGetValue("Data Source", out var dataSourceValue))
            {
                return dataSourceValue?.ToString();
            }

            if (sqlConnectionStringBuilder.TryGetValue("Server", out var serverValue))
            {
                return serverValue?.ToString();
            }

            return null;
        }

        private static bool IsLocalDbDataSource(string? dataSource) =>
            dataSource?.Contains("(localdb)", StringComparison.OrdinalIgnoreCase) == true ||
            dataSource?.Contains("mssqllocaldb", StringComparison.OrdinalIgnoreCase) == true;
    }
}
