using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "SellerOrder",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OrderItem",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "preparing");

            migrationBuilder.CreateTable(
                name: "ReturnRequest",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    SellerOrderId = table.Column<int>(type: "int", nullable: false),
                    BuyerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnRequest_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReturnRequest_SellerOrder_SellerOrderId",
                        column: x => x.SellerOrderId,
                        principalTable: "SellerOrder",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReturnRequestItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReturnRequestId = table.Column<int>(type: "int", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReturnRequestItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReturnRequestItem_OrderItem_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReturnRequestItem_ReturnRequest_ReturnRequestId",
                        column: x => x.ReturnRequestId,
                        principalTable: "ReturnRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Order_CreatedAt",
                table: "Order",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Order_Status",
                table: "Order",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequest_OrderId",
                table: "ReturnRequest",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequest_SellerOrderId",
                table: "ReturnRequest",
                column: "SellerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequest_Status",
                table: "ReturnRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItem_OrderItemId",
                table: "ReturnRequestItem",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequestItem_ReturnRequestId",
                table: "ReturnRequestItem",
                column: "ReturnRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReturnRequestItem");

            migrationBuilder.DropTable(
                name: "ReturnRequest");

            migrationBuilder.DropIndex(
                name: "IX_Order_CreatedAt",
                table: "Order");

            migrationBuilder.DropIndex(
                name: "IX_Order_Status",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "SellerOrder");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OrderItem");
        }
    }
}
