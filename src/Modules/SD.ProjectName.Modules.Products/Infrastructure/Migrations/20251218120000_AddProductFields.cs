using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Products.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ProductModel",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SellerId",
                table: "ProductModel",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProductModel",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "draft");

            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "ProductModel",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "ProductModel");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "ProductModel");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProductModel");

            migrationBuilder.DropColumn(
                name: "Stock",
                table: "ProductModel");
        }
    }
}
