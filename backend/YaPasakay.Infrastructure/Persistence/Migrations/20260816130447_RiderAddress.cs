using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiderAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AddressBarangayId",
                table: "RiderProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressDetails",
                table: "RiderProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullAddress",
                table: "RiderProfiles",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RiderProfiles_AddressBarangayId",
                table: "RiderProfiles",
                column: "AddressBarangayId");

            migrationBuilder.AddForeignKey(
                name: "FK_RiderProfiles_Barangays_AddressBarangayId",
                table: "RiderProfiles",
                column: "AddressBarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiderProfiles_Barangays_AddressBarangayId",
                table: "RiderProfiles");

            migrationBuilder.DropIndex(
                name: "IX_RiderProfiles_AddressBarangayId",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "AddressBarangayId",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "AddressDetails",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "FullAddress",
                table: "RiderProfiles");
        }
    }
}
