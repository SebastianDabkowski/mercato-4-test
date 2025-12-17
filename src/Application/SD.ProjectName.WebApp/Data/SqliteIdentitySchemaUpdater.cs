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

        var existingIndexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """PRAGMA index_list("AspNetUsers");""";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                existingIndexes.Add(reader.GetString(1));
            }
        }

        const string defaultAccountStatus = "Unverified";
        const string defaultAccountType = "Buyer";
        const string defaultKycStatus = "NotStarted";
        const string defaultTwoFactorMethod = "None";
        const string defaultSellerType = "Individual";

        var statusName = Enum.GetName(typeof(AccountStatus), AccountStatus.Unverified) ?? defaultAccountStatus;
        var typeName = Enum.GetName(typeof(AccountType), AccountType.Buyer) ?? defaultAccountType;
        var kycStatusName = Enum.GetName(typeof(KycStatus), KycStatus.NotStarted) ?? defaultKycStatus;
        var twoFactorMethodName = Enum.GetName(typeof(TwoFactorMethod), TwoFactorMethod.None) ?? defaultTwoFactorMethod;
        var sellerTypeName = Enum.GetName(typeof(SellerType), SellerType.Individual) ?? defaultSellerType;

        if (!string.Equals(statusName, defaultAccountStatus, StringComparison.Ordinal) ||
            !string.Equals(typeName, defaultAccountType, StringComparison.Ordinal) ||
            !string.Equals(kycStatusName, defaultKycStatus, StringComparison.Ordinal) ||
            !string.Equals(twoFactorMethodName, defaultTwoFactorMethod, StringComparison.Ordinal) ||
            !string.Equals(sellerTypeName, defaultSellerType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Default identity column values changed; update SqliteIdentitySchemaUpdater defaults.");
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

        if (!existingColumns.Contains("SellerType"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""SellerType"" TEXT NOT NULL DEFAULT 'Individual';";
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

        if (!existingColumns.Contains("VerificationRegistrationNumber"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""VerificationRegistrationNumber"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("VerificationAddress"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""VerificationAddress"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("VerificationContactPerson"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""VerificationContactPerson"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("VerificationPersonalIdNumber"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""VerificationPersonalIdNumber"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("StoreName"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""StoreName"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("StoreDescription"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""StoreDescription"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("StoreContactEmail"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""StoreContactEmail"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("StoreContactPhone"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""StoreContactPhone"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("StoreWebsiteUrl"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""StoreWebsiteUrl"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("StoreLogoPath"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""StoreLogoPath"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TermsAcceptedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TermsAcceptedAt"" TEXT NOT NULL DEFAULT '2025-12-14 00:00:00+00:00';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("EmailVerificationSentAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""EmailVerificationSentAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("EmailVerifiedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""EmailVerifiedAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("RequiresKyc"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""RequiresKyc"" INTEGER NOT NULL DEFAULT 0;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("KycStatus"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""KycStatus"" TEXT NOT NULL DEFAULT 'NotStarted';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("KycSubmittedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""KycSubmittedAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("KycApprovedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""KycApprovedAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TwoFactorMethod"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TwoFactorMethod"" TEXT NOT NULL DEFAULT 'None';";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TwoFactorConfiguredAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TwoFactorConfiguredAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TwoFactorLastUsedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TwoFactorLastUsedAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingColumns.Contains("TwoFactorRecoveryCodesGeneratedAt"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = @"ALTER TABLE ""AspNetUsers"" ADD COLUMN ""TwoFactorRecoveryCodesGeneratedAt"" TEXT NULL;";
            alter.ExecuteNonQuery();
        }

        if (!existingIndexes.Contains("IX_AspNetUsers_StoreName"))
        {
            using var index = connection.CreateCommand();
            index.CommandText = """CREATE UNIQUE INDEX IF NOT EXISTS "IX_AspNetUsers_StoreName" ON "AspNetUsers" ("StoreName" COLLATE NOCASE) WHERE "StoreName" IS NOT NULL;""";
            index.ExecuteNonQuery();
        }

        EnsureLoginAuditTable(connection);
        EnsureDataProtectionKeysTable(connection);
    }

    private static void EnsureLoginAuditTable(DbConnection connection)
    {
        using var checkTable = connection.CreateCommand();
        checkTable.CommandText = """SELECT name FROM sqlite_master WHERE type='table' AND name='LoginAuditEvents';""";
        var exists = checkTable.ExecuteScalar() != null;

        if (exists)
        {
            return;
        }

        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
CREATE TABLE IF NOT EXISTS "LoginAuditEvents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_LoginAuditEvents" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "EventType" TEXT NOT NULL,
    "Succeeded" INTEGER NOT NULL,
    "IsUnusual" INTEGER NOT NULL,
    "IpAddress" TEXT NULL,
    "UserAgent" TEXT NULL,
    "Reason" TEXT NULL,
    "OccurredAt" TEXT NOT NULL,
    "ExpiresAt" TEXT NULL
);
""";
            create.ExecuteNonQuery();
        }

        using (var indexUser = connection.CreateCommand())
        {
            indexUser.CommandText = """CREATE INDEX IF NOT EXISTS "IX_LoginAuditEvents_UserId" ON "LoginAuditEvents" ("UserId");""";
            indexUser.ExecuteNonQuery();
        }

        using (var indexOccurred = connection.CreateCommand())
        {
            indexOccurred.CommandText = """CREATE INDEX IF NOT EXISTS "IX_LoginAuditEvents_OccurredAt" ON "LoginAuditEvents" ("OccurredAt");""";
            indexOccurred.ExecuteNonQuery();
        }
    }

    private static void EnsureDataProtectionKeysTable(DbConnection connection)
    {
        using var checkTable = connection.CreateCommand();
        checkTable.CommandText = """SELECT name FROM sqlite_master WHERE type='table' AND name='DataProtectionKeys';""";
        var exists = checkTable.ExecuteScalar() != null;

        if (exists)
        {
            return;
        }

        using var create = connection.CreateCommand();
        create.CommandText = """
CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY AUTOINCREMENT,
    "FriendlyName" TEXT NULL,
    "Xml" TEXT NULL
);
""";
        create.ExecuteNonQuery();
    }
}
