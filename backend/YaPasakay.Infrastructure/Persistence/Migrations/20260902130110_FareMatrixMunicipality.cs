using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FareMatrixMunicipality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_OperatorId_VehicleType",
                table: "FareMatrices");

            migrationBuilder.AddColumn<Guid>(
                name: "MunicipalityId",
                table: "FareMatrices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE f
                SET MunicipalityId = COALESCE(
                    (
                        SELECT TOP 1 b.MunicipalityId
                        FROM OperatorBarangays ob
                        INNER JOIN Barangays b ON b.Id = ob.BarangayId
                        WHERE ob.OperatorId = f.OperatorId
                        ORDER BY b.MunicipalityId
                    ),
                    (
                        SELECT TOP 1 b.MunicipalityId
                        FROM Operators o
                        INNER JOIN Barangays b ON b.Id = o.AddressBarangayId
                        WHERE o.Id = f.OperatorId
                    ),
                    (
                        SELECT TOP 1 m.Id
                        FROM Municipalities m
                        ORDER BY m.Name
                    )
                )
                FROM FareMatrices f
                WHERE f.MunicipalityId IS NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO FareMatrices (
                    Id, OperatorId, MunicipalityId, VehicleType, BaseFare, PerKm, MinimumFare, IncludedKm,
                    OperatorCommissionPercent, DriverCommissionPercent, IsActive, CreatedAtUtc, UpdatedAtUtc)
                SELECT
                    NEWID(),
                    src.OperatorId,
                    cities.MunicipalityId,
                    src.VehicleType,
                    src.BaseFare,
                    src.PerKm,
                    src.MinimumFare,
                    src.IncludedKm,
                    src.OperatorCommissionPercent,
                    src.DriverCommissionPercent,
                    src.IsActive,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM FareMatrices src
                INNER JOIN (
                    SELECT DISTINCT ob.OperatorId, b.MunicipalityId
                    FROM OperatorBarangays ob
                    INNER JOIN Barangays b ON b.Id = ob.BarangayId
                ) cities ON cities.OperatorId = src.OperatorId
                WHERE src.MunicipalityId IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM FareMatrices existing
                      WHERE existing.OperatorId = src.OperatorId
                        AND existing.VehicleType = src.VehicleType
                        AND existing.MunicipalityId = cities.MunicipalityId
                  )
                  AND src.Id = (
                      SELECT TOP 1 f2.Id
                      FROM FareMatrices f2
                      WHERE f2.OperatorId = src.OperatorId
                        AND f2.VehicleType = src.VehicleType
                        AND f2.MunicipalityId IS NOT NULL
                      ORDER BY f2.CreatedAtUtc, f2.Id
                  );

                INSERT INTO FarePassengerTiers (
                    Id, FareMatrixId, PassengerCount, BaseFare, PerKm, MinimumFare, IncludedKm, CreatedAtUtc, UpdatedAtUtc)
                SELECT
                    NEWID(),
                    clone.Id,
                    tier.PassengerCount,
                    tier.BaseFare,
                    tier.PerKm,
                    tier.MinimumFare,
                    tier.IncludedKm,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM FareMatrices clone
                INNER JOIN FareMatrices template
                    ON template.OperatorId = clone.OperatorId
                   AND template.VehicleType = clone.VehicleType
                   AND template.Id = (
                        SELECT TOP 1 f2.Id
                        FROM FareMatrices f2
                        WHERE f2.OperatorId = clone.OperatorId
                          AND f2.VehicleType = clone.VehicleType
                        ORDER BY f2.CreatedAtUtc, f2.Id
                   )
                INNER JOIN FarePassengerTiers tier ON tier.FareMatrixId = template.Id
                WHERE clone.Id <> template.Id
                  AND NOT EXISTS (
                      SELECT 1 FROM FarePassengerTiers t WHERE t.FareMatrixId = clone.Id
                  );

                INSERT INTO FareSurcharges (
                    Id, FareMatrixId, Kind, Name, Amount, WindowStart, WindowEnd, RangeStartUtc, RangeEndUtc, IsActive, CreatedAtUtc, UpdatedAtUtc)
                SELECT
                    NEWID(),
                    clone.Id,
                    surcharge.Kind,
                    surcharge.Name,
                    surcharge.Amount,
                    surcharge.WindowStart,
                    surcharge.WindowEnd,
                    surcharge.RangeStartUtc,
                    surcharge.RangeEndUtc,
                    surcharge.IsActive,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM FareMatrices clone
                INNER JOIN FareMatrices template
                    ON template.OperatorId = clone.OperatorId
                   AND template.VehicleType = clone.VehicleType
                   AND template.Id = (
                        SELECT TOP 1 f2.Id
                        FROM FareMatrices f2
                        WHERE f2.OperatorId = clone.OperatorId
                          AND f2.VehicleType = clone.VehicleType
                        ORDER BY f2.CreatedAtUtc, f2.Id
                   )
                INNER JOIN FareSurcharges surcharge ON surcharge.FareMatrixId = template.Id
                WHERE clone.Id <> template.Id
                  AND NOT EXISTS (
                      SELECT 1 FROM FareSurcharges s WHERE s.FareMatrixId = clone.Id
                  );
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "MunicipalityId",
                table: "FareMatrices",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_MunicipalityId",
                table: "FareMatrices",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_OperatorId_VehicleType_MunicipalityId",
                table: "FareMatrices",
                columns: new[] { "OperatorId", "VehicleType", "MunicipalityId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FareMatrices_Municipalities_MunicipalityId",
                table: "FareMatrices",
                column: "MunicipalityId",
                principalTable: "Municipalities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FareMatrices_Municipalities_MunicipalityId",
                table: "FareMatrices");

            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_MunicipalityId",
                table: "FareMatrices");

            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_OperatorId_VehicleType_MunicipalityId",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "MunicipalityId",
                table: "FareMatrices");

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_OperatorId_VehicleType",
                table: "FareMatrices",
                columns: new[] { "OperatorId", "VehicleType" },
                unique: true);
        }
    }
}
