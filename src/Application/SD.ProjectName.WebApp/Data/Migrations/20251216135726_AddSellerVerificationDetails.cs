using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerVerificationDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerType",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AddColumn<string>(
                name: "VerificationAddress",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationContactPerson",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationPersonalIdNumber",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationRegistrationNumber",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationAddress",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationContactPerson",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationPersonalIdNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VerificationRegistrationNumber",
                table: "AspNetUsers");
        }
    }
}
