using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.Modules.Cart.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProviderReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "PaymentSelection",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                table: "PaymentSelection",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentSelection_ProviderReference",
                table: "PaymentSelection",
                column: "ProviderReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentSelection_ProviderReference",
                table: "PaymentSelection");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "PaymentSelection");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                table: "PaymentSelection");
        }
    }
}
