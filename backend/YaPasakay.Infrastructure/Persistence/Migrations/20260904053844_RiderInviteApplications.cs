using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiderInviteApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RiderInviteLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiderInviteLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiderInviteLinks_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiderApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviteLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VehicleType = table.Column<int>(type: "int", nullable: false),
                    PlateNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VehicleModel = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LicenseType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    LicenseNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AddressBarangayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressDetails = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FullAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ProfilePhotoPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    LicensePhotoPath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    AcceptedPaymentMethods = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RiderProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiderApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiderApplications_Barangays_AddressBarangayId",
                        column: x => x.AddressBarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiderApplications_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiderApplications_RiderInviteLinks_InviteLinkId",
                        column: x => x.InviteLinkId,
                        principalTable: "RiderInviteLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiderApplications_AddressBarangayId",
                table: "RiderApplications",
                column: "AddressBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderApplications_InviteLinkId",
                table: "RiderApplications",
                column: "InviteLinkId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderApplications_OperatorId",
                table: "RiderApplications",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderApplications_OperatorId_Status",
                table: "RiderApplications",
                columns: new[] { "OperatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiderApplications_PhoneNumber",
                table: "RiderApplications",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_RiderInviteLinks_OperatorId",
                table: "RiderInviteLinks",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RiderInviteLinks_Token",
                table: "RiderInviteLinks",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RiderApplications");

            migrationBuilder.DropTable(
                name: "RiderInviteLinks");
        }
    }
}
