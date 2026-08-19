using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerHail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HailAtUtc",
                table: "CustomerProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HailRiderId",
                table: "CustomerProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_HailRiderId",
                table: "CustomerProfiles",
                column: "HailRiderId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerProfiles_RiderProfiles_HailRiderId",
                table: "CustomerProfiles",
                column: "HailRiderId",
                principalTable: "RiderProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerProfiles_RiderProfiles_HailRiderId",
                table: "CustomerProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CustomerProfiles_HailRiderId",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "HailAtUtc",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "HailRiderId",
                table: "CustomerProfiles");
        }
    }
}
