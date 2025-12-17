using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SD.ProjectName.WebApp.Data;

namespace SD.ProjectName.Tests.Products;

public class SqliteMigrationTests
{
    [Fact]
    public async Task Migrate_ShouldAddAccountStatusColumn_WhenExistingSqliteDbIsMissingColumns()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"account-migration-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var createCommand = connection.CreateCommand();
                createCommand.CommandText =
                    """
                    CREATE TABLE "AspNetUsers" (
                        "Id" TEXT NOT NULL PRIMARY KEY,
                        "UserName" TEXT NULL,
                        "NormalizedUserName" TEXT NULL,
                        "Email" TEXT NULL,
                        "NormalizedEmail" TEXT NULL,
                        "EmailConfirmed" INTEGER NOT NULL,
                        "PasswordHash" TEXT NULL,
                        "SecurityStamp" TEXT NULL,
                        "ConcurrencyStamp" TEXT NULL,
                        "PhoneNumber" TEXT NULL,
                        "PhoneNumberConfirmed" INTEGER NOT NULL,
                        "TwoFactorEnabled" INTEGER NOT NULL,
                        "LockoutEnd" TEXT NULL,
                        "LockoutEnabled" INTEGER NOT NULL,
                        "AccessFailedCount" INTEGER NOT NULL
                    );
                    """;
                await createCommand.ExecuteNonQueryAsync();
            }

            await using (var context = new ApplicationDbContext(options))
            {
                SqliteIdentitySchemaUpdater.EnsureIdentityColumns(context.Database.GetDbConnection());
            }

            await using var verificationConnection = new SqliteConnection(connectionString);
            await verificationConnection.OpenAsync();
            var verificationCommand = verificationConnection.CreateCommand();
            verificationCommand.CommandText = """PRAGMA table_info("AspNetUsers");""";

            var columns = new List<string>();
            await using var reader = await verificationCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }

            Assert.Contains("AccountStatus", columns);
            Assert.Contains("AccountType", columns);
            Assert.Contains("TermsAcceptedAt", columns);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task EnsureIdentityColumns_ShouldCreateDataProtectionKeysTable_WhenMissing()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"dataprotection-migration-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";
        const string dataProtectionTableName = "DataProtectionKeys";

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var createCommand = connection.CreateCommand();
                createCommand.CommandText =
                    """
                    CREATE TABLE "AspNetUsers" (
                        "Id" TEXT NOT NULL PRIMARY KEY,
                        "UserName" TEXT NULL,
                        "NormalizedUserName" TEXT NULL,
                        "Email" TEXT NULL,
                        "NormalizedEmail" TEXT NULL,
                        "EmailConfirmed" INTEGER NOT NULL,
                        "PasswordHash" TEXT NULL,
                        "SecurityStamp" TEXT NULL,
                        "ConcurrencyStamp" TEXT NULL,
                        "PhoneNumber" TEXT NULL,
                        "PhoneNumberConfirmed" INTEGER NOT NULL,
                        "TwoFactorEnabled" INTEGER NOT NULL,
                        "LockoutEnd" TEXT NULL,
                        "LockoutEnabled" INTEGER NOT NULL,
                        "AccessFailedCount" INTEGER NOT NULL
                    );
                    """;
                await createCommand.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .Options;

            await using (var context = new ApplicationDbContext(options))
            {
                SqliteIdentitySchemaUpdater.EnsureIdentityColumns(context.Database.GetDbConnection());
            }

            await using var verificationConnection = new SqliteConnection(connectionString);
            await verificationConnection.OpenAsync();
            var verificationCommand = verificationConnection.CreateCommand();
            verificationCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;";
            verificationCommand.Parameters.AddWithValue("@tableName", dataProtectionTableName);

            var result = await verificationCommand.ExecuteScalarAsync();

            Assert.Equal(dataProtectionTableName, result?.ToString());
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public void InitializeDatabase_ShouldSucceed_WhenIdentityTablesAlreadyExist()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"existing-identity-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath}";

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connectionString)
                .Options;

            using (var setupContext = new ApplicationDbContext(options))
            {
                setupContext.Database.EnsureCreated();
            }

            using var context = new ApplicationDbContext(options);

            var exception = Record.Exception(() =>
            {
                context.Database.EnsureCreated();
                SqliteIdentitySchemaUpdater.EnsureIdentityColumns(context.Database.GetDbConnection());
            });

            Assert.Null(exception);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
