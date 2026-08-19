using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VehicleCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CommissionPercent",
                table: "Operators",
                newName: "MotorcycleCommissionPercent");

            migrationBuilder.AddColumn<decimal>(
                name: "TricycleCommissionPercent",
                table: "Operators",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 5m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TricycleCommissionPercent",
                table: "Operators");

            migrationBuilder.RenameColumn(
                name: "MotorcycleCommissionPercent",
                table: "Operators",
                newName: "CommissionPercent");
        }
    }
}
