using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperatorBills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MotorcycleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TricycleAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TripCount = table.Column<int>(type: "int", nullable: false),
                    PeriodFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisabledOperator = table.Column<bool>(type: "bit", nullable: false),
                    NotifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorBills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorBills_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperatorNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorNotifications_OperatorBills_BillId",
                        column: x => x.BillId,
                        principalTable: "OperatorBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperatorNotifications_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_BillId",
                table: "Trips",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorBills_Number",
                table: "OperatorBills",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorBills_OperatorId_CreatedAtUtc",
                table: "OperatorBills",
                columns: new[] { "OperatorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorNotifications_BillId",
                table: "OperatorNotifications",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorNotifications_OperatorId_CreatedAtUtc",
                table: "OperatorNotifications",
                columns: new[] { "OperatorId", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_OperatorBills_BillId",
                table: "Trips",
                column: "BillId",
                principalTable: "OperatorBills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_OperatorBills_BillId",
                table: "Trips");

            migrationBuilder.DropTable(
                name: "OperatorNotifications");

            migrationBuilder.DropTable(
                name: "OperatorBills");

            migrationBuilder.DropIndex(
                name: "IX_Trips_BillId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "BillId",
                table: "Trips");
        }
    }
}
