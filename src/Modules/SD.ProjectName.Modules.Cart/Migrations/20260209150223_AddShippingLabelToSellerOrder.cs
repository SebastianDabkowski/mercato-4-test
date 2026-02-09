using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingLabelToSellerOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ShippingLabel",
                table: "SellerOrder",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingLabelContentType",
                table: "SellerOrder",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippingLabelFileName",
                table: "SellerOrder",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShippingLabel",
                table: "SellerOrder");

            migrationBuilder.DropColumn(
                name: "ShippingLabelContentType",
                table: "SellerOrder");

            migrationBuilder.DropColumn(
                name: "ShippingLabelFileName",
                table: "SellerOrder");
        }
    }
}
