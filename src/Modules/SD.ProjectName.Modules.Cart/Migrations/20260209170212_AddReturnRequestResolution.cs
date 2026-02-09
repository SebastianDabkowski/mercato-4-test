using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddReturnRequestResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "ReturnRequest",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReference",
                table: "ReturnRequest",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundStatus",
                table: "ReturnRequest",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "not_required");

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "ReturnRequest",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "ReturnRequest",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "ReturnRequest");

            migrationBuilder.DropColumn(
                name: "RefundReference",
                table: "ReturnRequest");

            migrationBuilder.DropColumn(
                name: "RefundStatus",
                table: "ReturnRequest");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "ReturnRequest");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "ReturnRequest");
        }
    }
}
