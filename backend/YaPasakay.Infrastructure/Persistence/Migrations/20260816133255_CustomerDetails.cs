using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteRequestReason",
                table: "CustomerProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeleteRequestedAtUtc",
                table: "CustomerProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeleteResolutionNote",
                table: "CustomerProfiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeleteResolvedAtUtc",
                table: "CustomerProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeleteStatus",
                table: "CustomerProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "CustomerProfiles",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "CustomerProfiles",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "CustomerProfiles",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CustomerId",
                table: "Trips",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_CustomerProfiles_CustomerId",
                table: "Trips",
                column: "CustomerId",
                principalTable: "CustomerProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_CustomerProfiles_CustomerId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_CustomerId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeleteRequestReason",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "DeleteRequestedAtUtc",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "DeleteResolutionNote",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "DeleteResolvedAtUtc",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "DeleteStatus",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "CustomerProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "CustomerProfiles");
        }
    }
}
