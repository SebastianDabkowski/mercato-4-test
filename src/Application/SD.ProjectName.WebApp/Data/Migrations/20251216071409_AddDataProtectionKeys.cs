using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace SD.ProjectName.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataProtectionKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.CreateTable(
                    name: "DataProtectionKeys",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                        FriendlyName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                        Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                    });
            }
            else
            {
                migrationBuilder.CreateTable(
                    name: "DataProtectionKeys",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "INTEGER", nullable: false)
                            .Annotation("Sqlite:Autoincrement", true),
                        FriendlyName = table.Column<string>(type: "TEXT", nullable: true),
                        Xml = table.Column<string>(type: "TEXT", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                    });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");
        }
    }
}
