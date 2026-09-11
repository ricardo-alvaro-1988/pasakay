using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClearDefaultMerchantHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy admin defaults forced Mon–Sat 08:00–20:00 and Sunday Closed.
            // Merchants that never customized hours looked closed after 8pm PH.
            // Only clear rows that still match that exact silent default pattern.
            migrationBuilder.Sql(
                """
                ;WITH Defaulted AS (
                    SELECT MerchantId
                    FROM MerchantOperatingHours
                    GROUP BY MerchantId
                    HAVING COUNT(*) = 7
                       AND SUM(CASE
                             WHEN DayOfWeek = 0 AND IsClosed = 1
                                  AND OpenTime IS NULL AND CloseTime IS NULL THEN 1
                             ELSE 0 END) = 1
                       AND SUM(CASE
                             WHEN DayOfWeek BETWEEN 1 AND 6 AND IsClosed = 0
                                  AND OpenTime = '08:00:00' AND CloseTime = '20:00:00' THEN 1
                             ELSE 0 END) = 6
                )
                UPDATE h
                SET IsClosed = 0,
                    OpenTime = NULL,
                    CloseTime = NULL,
                    UpdatedAtUtc = GETUTCDATE()
                FROM MerchantOperatingHours h
                INNER JOIN Defaulted d ON d.MerchantId = h.MerchantId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
