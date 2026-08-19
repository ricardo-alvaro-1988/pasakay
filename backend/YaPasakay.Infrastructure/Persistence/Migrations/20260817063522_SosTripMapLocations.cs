using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SosTripMapLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DropoffLat",
                table: "Trips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DropoffLng",
                table: "Trips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLat",
                table: "Trips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PickupLng",
                table: "Trips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SosAtUtc",
                table: "SupportTickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SosLat",
                table: "SupportTickets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SosLng",
                table: "SupportTickets",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DropoffLat",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DropoffLng",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupLat",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupLng",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "SosAtUtc",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SosLat",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SosLng",
                table: "SupportTickets");
        }
    }
}
