using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorEmployeeAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessGroups_Name",
                table: "AccessGroups");

            migrationBuilder.AddColumn<bool>(
                name: "IsMainOperator",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing operator company logins become main operators (full module access).
            migrationBuilder.Sql("""
                UPDATE Users SET IsMainOperator = 1 WHERE Role = 2;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "OperatorId",
                table: "AccessGroups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroups_Name",
                table: "AccessGroups",
                column: "Name",
                unique: true,
                filter: "[OperatorId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroups_OperatorId_Name",
                table: "AccessGroups",
                columns: new[] { "OperatorId", "Name" },
                unique: true,
                filter: "[OperatorId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_AccessGroups_Operators_OperatorId",
                table: "AccessGroups",
                column: "OperatorId",
                principalTable: "Operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccessGroups_Operators_OperatorId",
                table: "AccessGroups");

            migrationBuilder.DropIndex(
                name: "IX_AccessGroups_Name",
                table: "AccessGroups");

            migrationBuilder.DropIndex(
                name: "IX_AccessGroups_OperatorId_Name",
                table: "AccessGroups");

            migrationBuilder.DropColumn(
                name: "IsMainOperator",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OperatorId",
                table: "AccessGroups");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGroups_Name",
                table: "AccessGroups",
                column: "Name",
                unique: true);
        }
    }
}
