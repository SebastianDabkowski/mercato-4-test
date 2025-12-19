using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Products.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSkuAndImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "ProductModel",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ProductImportJob",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SellerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    CreatedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorReport = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImportJob", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImportJob_SellerId",
                table: "ProductImportJob",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImportJob");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "ProductModel");
        }
    }
}
