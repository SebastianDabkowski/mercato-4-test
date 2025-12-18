using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerInternalUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerRole",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "StoreOwner");

            migrationBuilder.AddColumn<string>(
                name: "StoreOwnerId",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_StoreOwnerId",
                table: "AspNetUsers",
                column: "StoreOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_StoreOwnerId",
                table: "AspNetUsers",
                column: "StoreOwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_StoreOwnerId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_StoreOwnerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SellerRole",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreOwnerId",
                table: "AspNetUsers");
        }
    }
}
