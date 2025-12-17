using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerOnboardingWizard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnboardingCompleted",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingStep",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountNumber",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBeneficiaryName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingCompleted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OnboardingStep",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PayoutAccountNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PayoutBankName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PayoutBeneficiaryName",
                table: "AspNetUsers");
        }
    }
}
