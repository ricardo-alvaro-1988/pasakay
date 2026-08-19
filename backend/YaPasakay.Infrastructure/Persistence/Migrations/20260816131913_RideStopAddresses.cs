using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RideStopAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Pickup",
                table: "Trips",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "Dropoff",
                table: "Trips",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);

            migrationBuilder.AddColumn<Guid>(
                name: "DropoffBarangayId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DropoffDetails",
                table: "Trips",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PickupBarangayId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupDetails",
                table: "Trips",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DropoffBarangayId",
                table: "Trips",
                column: "DropoffBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_PickupBarangayId",
                table: "Trips",
                column: "PickupBarangayId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Barangays_DropoffBarangayId",
                table: "Trips",
                column: "DropoffBarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Barangays_PickupBarangayId",
                table: "Trips",
                column: "PickupBarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Barangays_DropoffBarangayId",
                table: "Trips");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Barangays_PickupBarangayId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_DropoffBarangayId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_PickupBarangayId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DropoffBarangayId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DropoffDetails",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupBarangayId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupDetails",
                table: "Trips");

            migrationBuilder.AlterColumn<string>(
                name: "Pickup",
                table: "Trips",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);

            migrationBuilder.AlterColumn<string>(
                name: "Dropoff",
                table: "Trips",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);
        }
    }
}
