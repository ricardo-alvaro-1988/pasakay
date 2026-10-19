using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhoneSignInEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PhoneSignInEnabled",
                table: "PlatformBrandSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneSignInEnabled",
                table: "PlatformBrandSettings");
        }
    }
}
