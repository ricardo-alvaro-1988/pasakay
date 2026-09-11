using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PabiliSurcharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PabiliSurcharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PabiliMatrixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_PabiliSurcharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PabiliSurcharges_PabiliMatrices_PabiliMatrixId",
                        column: x => x.PabiliMatrixId,
                        principalTable: "PabiliMatrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PabiliSurcharges_PabiliMatrixId",
                table: "PabiliSurcharges",
                column: "PabiliMatrixId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PabiliSurcharges");
        }
    }
}
