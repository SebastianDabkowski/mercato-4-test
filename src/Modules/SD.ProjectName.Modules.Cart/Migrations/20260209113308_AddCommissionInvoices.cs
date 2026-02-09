using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionInvoice",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SellerId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SellerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsCreditNote = table.Column<bool>(type: "bit", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionInvoice", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommissionInvoiceLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommissionInvoiceId = table.Column<int>(type: "int", nullable: false),
                    EscrowLedgerEntryId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCorrection = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionInvoiceLine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionInvoiceLine_CommissionInvoice_CommissionInvoiceId",
                        column: x => x.CommissionInvoiceId,
                        principalTable: "CommissionInvoice",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommissionInvoiceLine_EscrowLedger_EscrowLedgerEntryId",
                        column: x => x.EscrowLedgerEntryId,
                        principalTable: "EscrowLedger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommissionInvoice_SellerId_PeriodStart_PeriodEnd",
                table: "CommissionInvoice",
                columns: new[] { "SellerId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommissionInvoiceLine_CommissionInvoiceId",
                table: "CommissionInvoiceLine",
                column: "CommissionInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionInvoiceLine_EscrowLedgerEntryId",
                table: "CommissionInvoiceLine",
                column: "EscrowLedgerEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionInvoiceLine");

            migrationBuilder.DropTable(
                name: "CommissionInvoice");
        }
    }
}
