using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace SD.ProjectName.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorReadinessAndLoginAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.AddColumn<string>(
                    name: "TwoFactorMethod",
                    table: "AspNetUsers",
                    type: "nvarchar(32)",
                    maxLength: 32,
                    nullable: false,
                    defaultValue: "None");

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "TwoFactorConfiguredAt",
                    table: "AspNetUsers",
                    type: "datetimeoffset",
                    nullable: true);

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "TwoFactorLastUsedAt",
                    table: "AspNetUsers",
                    type: "datetimeoffset",
                    nullable: true);

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "TwoFactorRecoveryCodesGeneratedAt",
                    table: "AspNetUsers",
                    type: "datetimeoffset",
                    nullable: true);

                migrationBuilder.CreateTable(
                    name: "LoginAuditEvents",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                        UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                        EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                        Succeeded = table.Column<bool>(type: "bit", nullable: false),
                        IsUnusual = table.Column<bool>(type: "bit", nullable: false),
                        IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                        UserAgent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                        Reason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                        OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                        ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_LoginAuditEvents", x => x.Id);
                    });
            }
            else
            {
                migrationBuilder.AddColumn<string>(
                    name: "TwoFactorMethod",
                    table: "AspNetUsers",
                    type: "TEXT",
                    maxLength: 32,
                    nullable: false,
                    defaultValue: "None");

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "TwoFactorConfiguredAt",
                    table: "AspNetUsers",
                    type: "TEXT",
                    nullable: true);

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "TwoFactorLastUsedAt",
                    table: "AspNetUsers",
                    type: "TEXT",
                    nullable: true);

                migrationBuilder.AddColumn<DateTimeOffset>(
                    name: "TwoFactorRecoveryCodesGeneratedAt",
                    table: "AspNetUsers",
                    type: "TEXT",
                    nullable: true);

                migrationBuilder.CreateTable(
                    name: "LoginAuditEvents",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "INTEGER", nullable: false)
                            .Annotation("Sqlite:Autoincrement", true),
                        UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                        EventType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                        Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                        IsUnusual = table.Column<bool>(type: "INTEGER", nullable: false),
                        IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                        UserAgent = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                        Reason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                        OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                        ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_LoginAuditEvents", x => x.Id);
                    });
            }

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_OccurredAt",
                table: "LoginAuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_ExpiresAt",
                table: "LoginAuditEvents",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoginAuditEvents_UserId",
                table: "LoginAuditEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginAuditEvents");

            migrationBuilder.DropColumn(
                name: "TwoFactorConfiguredAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorLastUsedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorMethod",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorRecoveryCodesGeneratedAt",
                table: "AspNetUsers");
        }
    }
}
