using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VehicleOfferingLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleOfferingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MunicipalityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    VehicleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VehicleName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsOffered = table.Column<bool>(type: "bit", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AcceptedTerms = table.Column<bool>(type: "bit", nullable: false),
                    TermsVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleOfferingLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleOfferingLogs_Municipalities_MunicipalityId",
                        column: x => x.MunicipalityId,
                        principalTable: "Municipalities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleOfferingLogs_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleOfferingLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleOfferingLogs_VehicleCategories_VehicleCategoryId",
                        column: x => x.VehicleCategoryId,
                        principalTable: "VehicleCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleOfferingLogs_ActorUserId",
                table: "VehicleOfferingLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleOfferingLogs_AtUtc",
                table: "VehicleOfferingLogs",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleOfferingLogs_MunicipalityId",
                table: "VehicleOfferingLogs",
                column: "MunicipalityId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleOfferingLogs_OperatorId_AtUtc",
                table: "VehicleOfferingLogs",
                columns: new[] { "OperatorId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleOfferingLogs_VehicleCategoryId",
                table: "VehicleOfferingLogs",
                column: "VehicleCategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleOfferingLogs");
        }
    }
}
