using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminAccessGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccessGroupId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMainAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccessGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccessGroupPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessGroupPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessGroupPages_AccessGroups_AccessGroupId",
                        column: x => x.AccessGroupId,
                        principalTable: "AccessGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccessGroupId",
                table: "Users",
                column: "AccessGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroupPages_AccessGroupId_PageId",
                table: "AccessGroupPages",
                columns: new[] { "AccessGroupId", "PageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroups_Name",
                table: "AccessGroups",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_AccessGroups_AccessGroupId",
                table: "Users",
                column: "AccessGroupId",
                principalTable: "AccessGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_AccessGroups_AccessGroupId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AccessGroupPages");

            migrationBuilder.DropTable(
                name: "AccessGroups");

            migrationBuilder.DropIndex(
                name: "IX_Users_AccessGroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessGroupId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsMainAdmin",
                table: "Users");
        }
    }
}
