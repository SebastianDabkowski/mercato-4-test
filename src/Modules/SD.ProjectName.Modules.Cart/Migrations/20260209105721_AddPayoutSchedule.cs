using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayoutSchedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SellerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessingStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutSchedule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutScheduleItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayoutScheduleId = table.Column<int>(type: "int", nullable: false),
                    EscrowLedgerEntryId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutScheduleItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutScheduleItem_EscrowLedger_EscrowLedgerEntryId",
                        column: x => x.EscrowLedgerEntryId,
                        principalTable: "EscrowLedger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayoutScheduleItem_PayoutSchedule_PayoutScheduleId",
                        column: x => x.PayoutScheduleId,
                        principalTable: "PayoutSchedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutSchedule_SellerId",
                table: "PayoutSchedule",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutScheduleItem_EscrowLedgerEntryId",
                table: "PayoutScheduleItem",
                column: "EscrowLedgerEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutScheduleItem_PayoutScheduleId",
                table: "PayoutScheduleItem",
                column: "PayoutScheduleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutScheduleItem");

            migrationBuilder.DropTable(
                name: "PayoutSchedule");
        }
    }
}
