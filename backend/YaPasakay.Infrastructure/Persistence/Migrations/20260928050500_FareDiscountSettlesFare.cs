using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using YaPasakay.Infrastructure.Persistence;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260928050500_FareDiscountSettlesFare")]
    public partial class FareDiscountSettlesFare : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Trips
                SET Fare = CustomerFare
                WHERE FareDiscountAmount > 0
                  AND CustomerFare > 0
                  AND CustomerFare < Fare;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Trips
                SET Fare = Fare + FareDiscountAmount
                WHERE FareDiscountAmount > 0
                  AND CustomerFare > 0
                  AND Fare = CustomerFare;
                """);
        }
    }
}
