using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FareSurcharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
