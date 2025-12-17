using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SD.ProjectName.WebApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoreContactEmail",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreContactPhone",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreDescription",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreLogoPath",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreWebsiteUrl",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AspNetUsers_StoreName"" ON ""AspNetUsers"" (""StoreName"" COLLATE NOCASE) WHERE ""StoreName"" IS NOT NULL;");
            }
            else
            {
                migrationBuilder.CreateIndex(
                    name: "IX_AspNetUsers_StoreName",
                    table: "AspNetUsers",
                    column: "StoreName",
                    unique: true,
                    filter: "StoreName IS NOT NULL");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_StoreName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreContactEmail",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreContactPhone",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreDescription",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreLogoPath",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StoreWebsiteUrl",
                table: "AspNetUsers");
        }
    }
}
