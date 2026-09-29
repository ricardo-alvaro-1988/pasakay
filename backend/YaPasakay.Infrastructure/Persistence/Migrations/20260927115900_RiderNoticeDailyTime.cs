using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RiderNoticeDailyTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiderNotices_SentAtUtc_ScheduledAtUtc",
                table: "RiderNotices");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RiderNotices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NotifyMinuteOfDay",
                table: "RiderNotices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE RiderNotices
                SET NotifyMinuteOfDay = (DATEPART(hour, DATEADD(hour, 8, ScheduledAtUtc)) * 60)
                        + DATEPART(minute, DATEADD(hour, 8, ScheduledAtUtc)),
                    IsActive = CASE
                        WHEN SentAtUtc IS NOT NULL OR CancelledAtUtc IS NOT NULL THEN CAST(0 AS bit)
                        ELSE CAST(1 AS bit)
                    END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RiderNotices_IsActive_ScheduledAtUtc",
                table: "RiderNotices",
                columns: new[] { "IsActive", "ScheduledAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiderNotices_IsActive_ScheduledAtUtc",
                table: "RiderNotices");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RiderNotices");

            migrationBuilder.DropColumn(
                name: "NotifyMinuteOfDay",
                table: "RiderNotices");

            migrationBuilder.CreateIndex(
                name: "IX_RiderNotices_SentAtUtc_ScheduledAtUtc",
                table: "RiderNotices",
                columns: new[] { "SentAtUtc", "ScheduledAtUtc" });
        }
    }
}
