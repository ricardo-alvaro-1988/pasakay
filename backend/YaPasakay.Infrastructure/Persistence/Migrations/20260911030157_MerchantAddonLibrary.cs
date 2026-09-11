using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MerchantAddonLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AvailableFromTime",
                table: "MerchantProducts",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AvailableToTime",
                table: "MerchantProducts",
                type: "time",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MerchantAddonGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MinSelect = table.Column<int>(type: "int", nullable: false),
                    MaxSelect = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantAddonGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantAddonGroups_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MerchantAddonOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddonGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantAddonOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantAddonOptions_MerchantAddonGroups_AddonGroupId",
                        column: x => x.AddonGroupId,
                        principalTable: "MerchantAddonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MerchantProductAddons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddonGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantProductAddons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MerchantProductAddons_MerchantAddonGroups_AddonGroupId",
                        column: x => x.AddonGroupId,
                        principalTable: "MerchantAddonGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MerchantProductAddons_MerchantProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "MerchantProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantAddonGroups_MerchantId",
                table: "MerchantAddonGroups",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantAddonOptions_AddonGroupId",
                table: "MerchantAddonOptions",
                column: "AddonGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantProductAddons_AddonGroupId",
                table: "MerchantProductAddons",
                column: "AddonGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantProductAddons_ProductId_AddonGroupId",
                table: "MerchantProductAddons",
                columns: new[] { "ProductId", "AddonGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MerchantAddonOptions");

            migrationBuilder.DropTable(
                name: "MerchantProductAddons");

            migrationBuilder.DropTable(
                name: "MerchantAddonGroups");

            migrationBuilder.DropColumn(
                name: "AvailableFromTime",
                table: "MerchantProducts");

            migrationBuilder.DropColumn(
                name: "AvailableToTime",
                table: "MerchantProducts");
        }
    }
}
