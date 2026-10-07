using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RentalInquiryMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MatchedAtUtc",
                table: "CustomerRentalInquiries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MatchedListingId",
                table: "CustomerRentalInquiries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRentalInquiries_MatchedListingId",
                table: "CustomerRentalInquiries",
                column: "MatchedListingId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerRentalInquiries_CustomerCarListings_MatchedListingId",
                table: "CustomerRentalInquiries",
                column: "MatchedListingId",
                principalTable: "CustomerCarListings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerRentalInquiries_CustomerCarListings_MatchedListingId",
                table: "CustomerRentalInquiries");

            migrationBuilder.DropIndex(
                name: "IX_CustomerRentalInquiries_MatchedListingId",
                table: "CustomerRentalInquiries");

            migrationBuilder.DropColumn(
                name: "MatchedAtUtc",
                table: "CustomerRentalInquiries");

            migrationBuilder.DropColumn(
                name: "MatchedListingId",
                table: "CustomerRentalInquiries");
        }
    }
}
