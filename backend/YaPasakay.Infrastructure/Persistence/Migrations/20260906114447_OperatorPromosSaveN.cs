using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorPromosSaveN : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CustomerFare",
                table: "Trips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPromoSponsored",
                table: "Trips",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PromoDiscountAmount",
                table: "Trips",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoId",
                table: "Trips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("UPDATE Trips SET CustomerFare = Fare WHERE CustomerFare = 0;");

            migrationBuilder.CreateTable(
                name: "OperatorPromos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: true),
                    RedemptionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorPromos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorPromos_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromoRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoRedemptions_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromoRedemptions_OperatorPromos_PromoId",
                        column: x => x.PromoId,
                        principalTable: "OperatorPromos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromoRedemptions_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_PromoId",
                table: "Trips",
                column: "PromoId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorPromos_OperatorId_Code",
                table: "OperatorPromos",
                columns: new[] { "OperatorId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoRedemptions_CustomerId",
                table: "PromoRedemptions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoRedemptions_PromoId_CustomerId",
                table: "PromoRedemptions",
                columns: new[] { "PromoId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromoRedemptions_TripId",
                table: "PromoRedemptions",
                column: "TripId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_OperatorPromos_PromoId",
                table: "Trips",
                column: "PromoId",
                principalTable: "OperatorPromos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_OperatorPromos_PromoId",
                table: "Trips");

            migrationBuilder.DropTable(
                name: "PromoRedemptions");

            migrationBuilder.DropTable(
                name: "OperatorPromos");

            migrationBuilder.DropIndex(
                name: "IX_Trips_PromoId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "CustomerFare",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "IsPromoSponsored",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PromoDiscountAmount",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PromoId",
                table: "Trips");
        }
    }
}
