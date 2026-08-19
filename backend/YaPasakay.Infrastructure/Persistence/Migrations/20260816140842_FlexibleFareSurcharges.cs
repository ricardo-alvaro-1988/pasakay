using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleFareSurcharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomSurcharge",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "CustomSurchargeLabel",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "NightSurcharge",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "PeakSurcharge",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "RainSurcharge",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "RainSurchargeActive",
                table: "FareMatrices");

            migrationBuilder.CreateTable(
                name: "FareSurcharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FareMatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WindowStart = table.Column<TimeOnly>(type: "time", nullable: true),
                    WindowEnd = table.Column<TimeOnly>(type: "time", nullable: true),
                    RangeStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RangeEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FareSurcharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FareSurcharges_FareMatrices_FareMatrixId",
                        column: x => x.FareMatrixId,
                        principalTable: "FareMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FareSurcharges_FareMatrixId",
                table: "FareSurcharges",
                column: "FareMatrixId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FareSurcharges");

            migrationBuilder.AddColumn<decimal>(
                name: "CustomSurcharge",
                table: "FareMatrices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CustomSurchargeLabel",
                table: "FareMatrices",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NightSurcharge",
                table: "FareMatrices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PeakSurcharge",
                table: "FareMatrices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RainSurcharge",
                table: "FareMatrices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RainSurchargeActive",
                table: "FareMatrices",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
