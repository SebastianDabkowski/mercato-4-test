using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SellerOrderId",
                table: "OrderShippingSelection",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SellerOrderId",
                table: "OrderItem",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountTotal",
                table: "Order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PromoCode",
                table: "Order",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PromoCode",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DiscountType = table.Column<int>(type: "int", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MinimumSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ValidUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromoSelection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuyerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PromoCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoSelection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SellerOrder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    SellerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SellerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ItemsSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShippingTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellerOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellerOrder_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PromoCode",
                columns: new[] { "Id", "Code", "Description", "DiscountType", "DiscountValue", "IsActive", "MinimumSubtotal", "SellerId", "ValidFrom", "ValidUntil" },
                values: new object[,]
                {
                    { 1, "WELCOME10", "10% off any order over 50", 0, 0.10m, true, 50m, null, new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2030, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 2, "SELLER5", "5 currency units off Seller One items over 20", 1, 5m, true, 20m, "seller-1", new DateTimeOffset(new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2030, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderShippingSelection_SellerOrderId",
                table: "OrderShippingSelection",
                column: "SellerOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_SellerOrderId",
                table: "OrderItem",
                column: "SellerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCode_Code",
                table: "PromoCode",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoSelection_BuyerId",
                table: "PromoSelection",
                column: "BuyerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrder_OrderId",
                table: "SellerOrder",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SellerOrder_SellerId",
                table: "SellerOrder",
                column: "SellerId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItem_SellerOrder_SellerOrderId",
                table: "OrderItem",
                column: "SellerOrderId",
                principalTable: "SellerOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderShippingSelection_SellerOrder_SellerOrderId",
                table: "OrderShippingSelection",
                column: "SellerOrderId",
                principalTable: "SellerOrder",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItem_SellerOrder_SellerOrderId",
                table: "OrderItem");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderShippingSelection_SellerOrder_SellerOrderId",
                table: "OrderShippingSelection");

            migrationBuilder.DropTable(
                name: "PromoCode");

            migrationBuilder.DropTable(
                name: "PromoSelection");

            migrationBuilder.DropTable(
                name: "SellerOrder");

            migrationBuilder.DropIndex(
                name: "IX_OrderShippingSelection_SellerOrderId",
                table: "OrderShippingSelection");

            migrationBuilder.DropIndex(
                name: "IX_OrderItem_SellerOrderId",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "SellerOrderId",
                table: "OrderShippingSelection");

            migrationBuilder.DropColumn(
                name: "SellerOrderId",
                table: "OrderItem");

            migrationBuilder.DropColumn(
                name: "DiscountTotal",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "PromoCode",
                table: "Order");
        }
    }
}
