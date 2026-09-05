using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiderCredibilityAndCancelledBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelledBy",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CredibilityScore",
                table: "RiderProfiles",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "RiderCancelCount",
                table: "RiderProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE Trips
                SET CancelledBy = 1
                WHERE Status = 2 AND CancelReason LIKE N'Customer cancelled%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CredibilityScore",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "RiderCancelCount",
                table: "RiderProfiles");
        }
    }
}
