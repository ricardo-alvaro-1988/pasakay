using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VehicleCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VehicleCategoryId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleCategoryId",
                table: "RiderProfiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleCategoryId",
                table: "RiderApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleCategoryId",
                table: "FareMatrices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleCategoryId",
                table: "DeriveFareMatrices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehicleCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    MaxPassengers = table.Column<int>(type: "int", nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCargo = table.Column<bool>(type: "bit", nullable: false),
                    LegacyEnumValue = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleCategories_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OperatorBillVehicleLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorBillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VehicleName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorBillVehicleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorBillVehicleLines_OperatorBills_OperatorBillId",
                        column: x => x.OperatorBillId,
                        principalTable: "OperatorBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorBillVehicleLines_VehicleCategories_VehicleCategoryId",
                        column: x => x.VehicleCategoryId,
                        principalTable: "VehicleCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatorVehicleOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    MaxPassengers = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorVehicleOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorVehicleOffers_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorVehicleOffers_VehicleCategories_VehicleCategoryId",
                        column: x => x.VehicleCategoryId,
                        principalTable: "VehicleCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_VehicleCategoryId",
                table: "Trips",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderProfiles_VehicleCategoryId",
                table: "RiderProfiles",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderApplications_VehicleCategoryId",
                table: "RiderApplications",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FareMatrices_VehicleCategoryId",
                table: "FareMatrices",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeriveFareMatrices_VehicleCategoryId",
                table: "DeriveFareMatrices",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorBillVehicleLines_OperatorBillId",
                table: "OperatorBillVehicleLines",
                column: "OperatorBillId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorBillVehicleLines_VehicleCategoryId",
                table: "OperatorBillVehicleLines",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorVehicleOffers_OperatorId_VehicleCategoryId",
                table: "OperatorVehicleOffers",
                columns: new[] { "OperatorId", "VehicleCategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorVehicleOffers_VehicleCategoryId",
                table: "OperatorVehicleOffers",
                column: "VehicleCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleCategories_Code",
                table: "VehicleCategories",
                column: "Code",
                unique: true,
                filter: "[OperatorId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleCategories_OperatorId_Code",
                table: "VehicleCategories",
                columns: new[] { "OperatorId", "Code" },
                unique: true,
                filter: "[OperatorId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_DeriveFareMatrices_VehicleCategories_VehicleCategoryId",
                table: "DeriveFareMatrices",
                column: "VehicleCategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FareMatrices_VehicleCategories_VehicleCategoryId",
                table: "FareMatrices",
                column: "VehicleCategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RiderApplications_VehicleCategories_VehicleCategoryId",
                table: "RiderApplications",
                column: "VehicleCategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RiderProfiles_VehicleCategories_VehicleCategoryId",
                table: "RiderProfiles",
                column: "VehicleCategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_VehicleCategories_VehicleCategoryId",
                table: "Trips",
                column: "VehicleCategoryId",
                principalTable: "VehicleCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeriveFareMatrices_VehicleCategories_VehicleCategoryId",
                table: "DeriveFareMatrices");

            migrationBuilder.DropForeignKey(
                name: "FK_FareMatrices_VehicleCategories_VehicleCategoryId",
                table: "FareMatrices");

            migrationBuilder.DropForeignKey(
                name: "FK_RiderApplications_VehicleCategories_VehicleCategoryId",
                table: "RiderApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_RiderProfiles_VehicleCategories_VehicleCategoryId",
                table: "RiderProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_VehicleCategories_VehicleCategoryId",
                table: "Trips");

            migrationBuilder.DropTable(
                name: "OperatorBillVehicleLines");

            migrationBuilder.DropTable(
                name: "OperatorVehicleOffers");

            migrationBuilder.DropTable(
                name: "VehicleCategories");

            migrationBuilder.DropIndex(
                name: "IX_Trips_VehicleCategoryId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_RiderProfiles_VehicleCategoryId",
                table: "RiderProfiles");

            migrationBuilder.DropIndex(
                name: "IX_RiderApplications_VehicleCategoryId",
                table: "RiderApplications");

            migrationBuilder.DropIndex(
                name: "IX_FareMatrices_VehicleCategoryId",
                table: "FareMatrices");

            migrationBuilder.DropIndex(
                name: "IX_DeriveFareMatrices_VehicleCategoryId",
                table: "DeriveFareMatrices");

            migrationBuilder.DropColumn(
                name: "VehicleCategoryId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "VehicleCategoryId",
                table: "RiderProfiles");

            migrationBuilder.DropColumn(
                name: "VehicleCategoryId",
                table: "RiderApplications");

            migrationBuilder.DropColumn(
                name: "VehicleCategoryId",
                table: "FareMatrices");

            migrationBuilder.DropColumn(
                name: "VehicleCategoryId",
                table: "DeriveFareMatrices");
        }
    }
}
