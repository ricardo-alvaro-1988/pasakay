using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FareMatrixByCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_OperatorId_VehicleType_MunicipalityId",
                table: "FareMatrices");

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_OperatorId_MunicipalityId_VehicleCategoryId",
                table: "FareMatrices",
                columns: new[] { "OperatorId", "MunicipalityId", "VehicleCategoryId" },
                unique: true,
                filter: "[VehicleCategoryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_VehicleType",
                table: "FareMatrices",
                column: "VehicleType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_OperatorId_MunicipalityId_VehicleCategoryId",
                table: "FareMatrices");

            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_VehicleType",
                table: "FareMatrices");

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_OperatorId_VehicleType_MunicipalityId",
                table: "FareMatrices",
                columns: new[] { "OperatorId", "VehicleType", "MunicipalityId" },
                unique: true);
        }
    }
}
