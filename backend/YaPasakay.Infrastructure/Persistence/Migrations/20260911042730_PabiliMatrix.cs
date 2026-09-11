using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PabiliMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PabiliFareSystemCommissionPercent",
                table: "Operators",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<decimal>(
                name: "PabiliMarkupSystemCommissionPercent",
                table: "Operators",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.CreateTable(
                name: "PabiliMatrices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseFareAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KmScope = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    SucceedingKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FareOperatorCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    FareRiderCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MarkupOperatorCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MarkupRiderCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PabiliMatrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PabiliMatrices_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PabiliMatrices_OperatorId",
                table: "PabiliMatrices",
                column: "OperatorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PabiliMatrices");

            migrationBuilder.DropColumn(
                name: "PabiliFareSystemCommissionPercent",
                table: "Operators");

            migrationBuilder.DropColumn(
                name: "PabiliMarkupSystemCommissionPercent",
                table: "Operators");
        }
    }
}
