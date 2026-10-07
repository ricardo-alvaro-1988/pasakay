using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRentals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerCarListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    VehicleCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlateNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Seater = table.Column<int>(type: "int", nullable: false),
                    FrontImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    BackImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    LeftImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    RightImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    InsideImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AvailableMonday = table.Column<bool>(type: "bit", nullable: false),
                    AvailableTuesday = table.Column<bool>(type: "bit", nullable: false),
                    AvailableWednesday = table.Column<bool>(type: "bit", nullable: false),
                    AvailableThursday = table.Column<bool>(type: "bit", nullable: false),
                    AvailableFriday = table.Column<bool>(type: "bit", nullable: false),
                    AvailableSaturday = table.Column<bool>(type: "bit", nullable: false),
                    AvailableSunday = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCarListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerCarListings_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerCarListings_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerRentalInquiries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    VehicleCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduleFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduleToUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LocationDetails = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    LocationLat = table.Column<double>(type: "float", nullable: false),
                    LocationLng = table.Column<double>(type: "float", nullable: false),
                    BarangayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRentalInquiries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerRentalInquiries_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerRentalInquiries_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCarListings_CustomerId",
                table: "CustomerCarListings",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCarListings_OperatorId_CreatedAtUtc",
                table: "CustomerCarListings",
                columns: new[] { "OperatorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCarListings_OperatorId_Status",
                table: "CustomerCarListings",
                columns: new[] { "OperatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRentalInquiries_CustomerId",
                table: "CustomerRentalInquiries",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRentalInquiries_OperatorId_CreatedAtUtc",
                table: "CustomerRentalInquiries",
                columns: new[] { "OperatorId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRentalInquiries_OperatorId_Status",
                table: "CustomerRentalInquiries",
                columns: new[] { "OperatorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCarListings");

            migrationBuilder.DropTable(
                name: "CustomerRentalInquiries");
        }
    }
}
