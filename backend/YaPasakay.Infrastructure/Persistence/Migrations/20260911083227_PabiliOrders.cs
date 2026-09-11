using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PabiliOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PabiliOrderId",
                table: "RiderWalletTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PabiliOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MerchantName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    PickupAddress = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    PickupLat = table.Column<double>(type: "float", nullable: false),
                    PickupLng = table.Column<double>(type: "float", nullable: false),
                    DropoffAddress = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    DropoffLat = table.Column<double>(type: "float", nullable: false),
                    DropoffLng = table.Column<double>(type: "float", nullable: false),
                    DropoffBarangayId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DistanceKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GoodsSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GoodsBaseSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SurchargeTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CustomerTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PickedUpAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveringAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<int>(type: "int", nullable: false),
                    CancelReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FareSystemPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    FareOperatorPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    FareRiderPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MarkupSystemPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MarkupOperatorPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    MarkupRiderPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    FareSystemAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FareOperatorAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FareRiderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarkupSystemAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarkupOperatorAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarkupRiderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PabiliOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PabiliOrders_Barangays_DropoffBarangayId",
                        column: x => x.DropoffBarangayId,
                        principalTable: "Barangays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PabiliOrders_CustomerProfiles_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PabiliOrders_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PabiliOrders_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PabiliOrders_RiderProfiles_RiderId",
                        column: x => x.RiderId,
                        principalTable: "RiderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PabiliOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitBasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitSellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineBaseTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineSellingTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PabiliOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PabiliOrderItems_PabiliOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PabiliOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PabiliOrderOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OfferedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PabiliOrderOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PabiliOrderOffers_PabiliOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PabiliOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PabiliOrderOffers_RiderProfiles_RiderId",
                        column: x => x.RiderId,
                        principalTable: "RiderProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PabiliOrderItemAddons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddonOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitBasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitSellingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineBaseTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineSellingTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PabiliOrderItemAddons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PabiliOrderItemAddons_PabiliOrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "PabiliOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiderWalletTransactions_PabiliOrderId",
                table: "RiderWalletTransactions",
                column: "PabiliOrderId",
                unique: true,
                filter: "[PabiliOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrderItemAddons_OrderItemId",
                table: "PabiliOrderItemAddons",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrderItems_OrderId",
                table: "PabiliOrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrderOffers_OrderId_RiderId",
                table: "PabiliOrderOffers",
                columns: new[] { "OrderId", "RiderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrderOffers_RiderId_Status",
                table: "PabiliOrderOffers",
                columns: new[] { "RiderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_CustomerId",
                table: "PabiliOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_DropoffBarangayId",
                table: "PabiliOrders",
                column: "DropoffBarangayId");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_MerchantId",
                table: "PabiliOrders",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_OperatorId",
                table: "PabiliOrders",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_OperatorId_Status_CreatedAtUtc",
                table: "PabiliOrders",
                columns: new[] { "OperatorId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_Reference",
                table: "PabiliOrders",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_PabiliOrders_RiderId",
                table: "PabiliOrders",
                column: "RiderId");

            migrationBuilder.AddForeignKey(
                name: "FK_RiderWalletTransactions_PabiliOrders_PabiliOrderId",
                table: "RiderWalletTransactions",
                column: "PabiliOrderId",
                principalTable: "PabiliOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiderWalletTransactions_PabiliOrders_PabiliOrderId",
                table: "RiderWalletTransactions");

            migrationBuilder.DropTable(
                name: "PabiliOrderItemAddons");

            migrationBuilder.DropTable(
                name: "PabiliOrderOffers");

            migrationBuilder.DropTable(
                name: "PabiliOrderItems");

            migrationBuilder.DropTable(
                name: "PabiliOrders");

            migrationBuilder.DropIndex(
                name: "IX_RiderWalletTransactions_PabiliOrderId",
                table: "RiderWalletTransactions");

            migrationBuilder.DropColumn(
                name: "PabiliOrderId",
                table: "RiderWalletTransactions");
        }
    }
}
