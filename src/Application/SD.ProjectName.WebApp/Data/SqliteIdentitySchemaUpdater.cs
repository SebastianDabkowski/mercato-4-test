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

        void AddColumnIfMissing(string name, string definition)
        {
            if (existingColumns.Contains(name))
            {
                return;
            }

            using var alter = connection.CreateCommand();
            alter.CommandText = $@"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""{name}"" {definition};";
            alter.ExecuteNonQuery();
        }

        AddColumnIfMissing("AccountStatus", "TEXT NOT NULL DEFAULT 'Unverified'");
        AddColumnIfMissing("AccountType", "TEXT NOT NULL DEFAULT 'Buyer'");
        AddColumnIfMissing("CompanyName", "TEXT NULL");
        AddColumnIfMissing("FirstName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("LastName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing("TaxId", "TEXT NULL");
        AddColumnIfMissing("TermsAcceptedAt", "TEXT NOT NULL DEFAULT '2025-12-14 00:00:00+00:00'");
    }
}
