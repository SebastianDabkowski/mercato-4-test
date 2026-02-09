using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderStatusLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "SellerOrder",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "SellerOrder",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "Order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "SellerOrder");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "SellerOrder");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "Order");
        }
    }
}
