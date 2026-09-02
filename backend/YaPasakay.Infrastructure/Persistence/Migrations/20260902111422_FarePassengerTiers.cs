using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FarePassengerTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PassengerCount",
                table: "Trips",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "FarePassengerTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FareMatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_FarePassengerTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FarePassengerTiers_FareMatrices_FareMatrixId",
                        column: x => x.FareMatrixId,
                        principalTable: "FareMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FarePassengerTiers_FareMatrixId_PassengerCount",
                table: "FarePassengerTiers",
                columns: new[] { "FareMatrixId", "PassengerCount" },
                unique: true);

            migrationBuilder.Sql("""
                UPDATE Trips SET PassengerCount = 1 WHERE PassengerCount < 1;

                INSERT INTO FarePassengerTiers (Id, FareMatrixId, PassengerCount, BaseFare, PerKm, MinimumFare, IncludedKm, CreatedAtUtc, UpdatedAtUtc)
                SELECT NEWID(), f.Id, 1, f.BaseFare, f.PerKm, f.MinimumFare, f.IncludedKm, SYSUTCDATETIME(), NULL
                FROM FareMatrices f
                WHERE NOT EXISTS (
                    SELECT 1 FROM FarePassengerTiers t WHERE t.FareMatrixId = f.Id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FarePassengerTiers");

            migrationBuilder.DropColumn(
                name: "PassengerCount",
                table: "Trips");
        }
    }
}
