using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiderInviteKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "RiderInviteLinks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RiderInviteLinks_OperatorId_Kind",
                table: "RiderInviteLinks",
                columns: new[] { "OperatorId", "Kind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiderInviteLinks_OperatorId_Kind",
                table: "RiderInviteLinks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "RiderInviteLinks");
        }
    }
}
