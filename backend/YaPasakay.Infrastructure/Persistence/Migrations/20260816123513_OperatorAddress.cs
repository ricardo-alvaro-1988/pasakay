using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FullAddress",
                table: "Operators",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<Guid>(
                name: "AddressBarangayId",
                table: "Operators",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressDetails",
                table: "Operators",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Operators_AddressBarangayId",
                table: "Operators",
                column: "AddressBarangayId");

            migrationBuilder.AddForeignKey(
                name: "FK_Operators_Barangays_AddressBarangayId",
                table: "Operators",
                column: "AddressBarangayId",
                principalTable: "Barangays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Operators_Barangays_AddressBarangayId",
                table: "Operators");

            migrationBuilder.DropIndex(
                name: "IX_Operators_AddressBarangayId",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "AddressBarangayId",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "AddressDetails",
                table: "Operators");

            migrationBuilder.AlterColumn<string>(
                name: "FullAddress",
                table: "Operators",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);
        }
    }
}
