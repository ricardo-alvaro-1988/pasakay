using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MerchantDualPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                table: "MerchantProducts",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE [MerchantProducts] SET [SellingPrice] = [BasePrice];
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "BasePrice",
                table: "MerchantAddonOptions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.RenameColumn(
                name: "PriceDelta",
                table: "MerchantAddonOptions",
                newName: "SellingPrice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SellingPrice",
                table: "MerchantAddonOptions",
                newName: "PriceDelta");

            migrationBuilder.DropColumn(
                name: "BasePrice",
                table: "MerchantAddonOptions");

            migrationBuilder.DropColumn(
                name: "SellingPrice",
                table: "MerchantProducts");
        }
    }
}
