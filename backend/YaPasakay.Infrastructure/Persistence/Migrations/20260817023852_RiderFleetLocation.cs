using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiderFleetLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LastLat",
                table: "RiderProfiles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastLng",
                table: "RiderProfiles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationAtUtc",
                table: "RiderProfiles",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLat",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "LastLng",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "LastLocationAtUtc",
                table: "RiderProfiles");
        }
    }
}
