using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeriveFareZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeriveFareZoneId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeriveFareZones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    MaxDropoffKm = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    PolygonJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeriveFareZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeriveFareZones_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeriveFareMatrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeriveFareZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    BaseFare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumFare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    OperatorCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DriverCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeriveFareMatrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeriveFareMatrices_DeriveFareZones_DeriveFareZoneId",
                        column: x => x.DeriveFareZoneId,
                        principalTable: "DeriveFareZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeriveFarePassengerTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeriveFareMatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PassengerCount = table.Column<int>(type: "int", nullable: false),
                    BaseFare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinimumFare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeriveFarePassengerTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeriveFarePassengerTiers_DeriveFareMatrices_DeriveFareMatrixId",
                        column: x => x.DeriveFareMatrixId,
                        principalTable: "DeriveFareMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DeriveFareZoneId",
                table: "Trips",
                column: "DeriveFareZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_DeriveFareMatrices_DeriveFareZoneId_VehicleType",
                table: "DeriveFareMatrices",
                columns: new[] { "DeriveFareZoneId", "VehicleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeriveFarePassengerTiers_DeriveFareMatrixId_PassengerCount",
                table: "DeriveFarePassengerTiers",
                columns: new[] { "DeriveFareMatrixId", "PassengerCount" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeriveFareZones_OperatorId_Name",
                table: "DeriveFareZones",
                columns: new[] { "OperatorId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_DeriveFareZones_DeriveFareZoneId",
                table: "Trips",
                column: "DeriveFareZoneId",
                principalTable: "DeriveFareZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_DeriveFareZones_DeriveFareZoneId",
                table: "Trips");

            migrationBuilder.DropTable(
                name: "DeriveFarePassengerTiers");

            migrationBuilder.DropTable(
                name: "DeriveFareMatrices");

            migrationBuilder.DropTable(
                name: "DeriveFareZones");

            migrationBuilder.DropIndex(
                name: "IX_Trips_DeriveFareZoneId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DeriveFareZoneId",
                table: "Trips");
        }
    }
}
