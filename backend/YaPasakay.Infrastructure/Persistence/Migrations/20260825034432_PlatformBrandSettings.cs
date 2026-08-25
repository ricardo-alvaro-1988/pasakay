using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformBrandSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformBrandSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LogoPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    FaviconPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ThemeId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformBrandSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformBrandSettings");
        }
    }
}
