using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AddedAt",
                table: "CartItem",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "BuyerId",
                table: "CartItem",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SellerName",
                table: "CartItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DeliveryAddress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Line1 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Line2 = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SavedToProfile = table.Column<bool>(type: "bit", nullable: false),
                    IsSelectedForCheckout = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAddress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartItem_BuyerId",
                table: "CartItem",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAddress_BuyerId",
                table: "DeliveryAddress",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAddress_IsSelectedForCheckout",
                table: "DeliveryAddress",
                column: "IsSelectedForCheckout");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryAddress");

            migrationBuilder.DropIndex(
                name: "IX_CartItem_BuyerId",
                table: "CartItem");

            migrationBuilder.DropColumn(
                name: "AddedAt",
                table: "CartItem");

            migrationBuilder.DropColumn(
                name: "BuyerId",
                table: "CartItem");

            migrationBuilder.DropColumn(
                name: "SellerName",
                table: "CartItem");
        }
    }
}
