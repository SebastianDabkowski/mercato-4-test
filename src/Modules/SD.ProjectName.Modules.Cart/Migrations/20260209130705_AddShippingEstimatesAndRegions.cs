using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingEstimatesAndRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryEstimate",
                table: "ShippingSelection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedCountryCodes",
                table: "ShippingRule",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedRegions",
                table: "ShippingRule",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryEstimate",
                table: "ShippingRule",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryEstimate",
                table: "OrderShippingSelection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryEstimate",
                table: "ShippingSelection");

            migrationBuilder.DropColumn(
                name: "AllowedCountryCodes",
                table: "ShippingRule");

            migrationBuilder.DropColumn(
                name: "AllowedRegions",
                table: "ShippingRule");

            migrationBuilder.DropColumn(
                name: "DeliveryEstimate",
                table: "ShippingRule");

            migrationBuilder.DropColumn(
                name: "DeliveryEstimate",
                table: "OrderShippingSelection");
        }
    }
}
