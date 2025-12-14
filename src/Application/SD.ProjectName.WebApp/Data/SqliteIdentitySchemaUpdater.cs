using System.Data.Common;

namespace SD.ProjectName.WebApp.Data;

public static class SqliteIdentitySchemaUpdater
{
    public static void EnsureIdentityColumns(DbConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """PRAGMA table_info("AspNetUsers");""";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (!existingColumns.Contains("AccountStatus"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""AccountStatus"" TEXT NOT NULL DEFAULT 'Unverified';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("AccountType"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""AccountType"" TEXT NOT NULL DEFAULT 'Buyer';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("CompanyName"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""CompanyName"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("FirstName"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""FirstName"" TEXT NOT NULL DEFAULT '';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("LastName"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""LastName"" TEXT NOT NULL DEFAULT '';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TaxId"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TaxId"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TermsAcceptedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TermsAcceptedAt"" TEXT NOT NULL DEFAULT (datetime('now'));";
            alter.ExecuteNonQuery();
        }
    }
}
