using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorPabiliBrowseCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperatorPabiliBrowseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorPabiliBrowseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorPabiliBrowseCategories_Operators_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorPabiliBrowseCategories_OperatorId_Name",
                table: "OperatorPabiliBrowseCategories",
                columns: new[] { "OperatorId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatorPabiliBrowseCategories_OperatorId_SortOrder",
                table: "OperatorPabiliBrowseCategories",
                columns: new[] { "OperatorId", "SortOrder" });

            migrationBuilder.Sql(
                """
                DECLARE @now datetime2 = SYSUTCDATETIME();
                INSERT INTO [OperatorPabiliBrowseCategories]
                    ([Id], [OperatorId], [Name], [IsActive], [SortOrder], [CreatedAtUtc], [UpdatedAtUtc])
                SELECT NEWID(), o.[Id], v.[Name], 1, v.[SortOrder], @now, NULL
                FROM [Operators] o
                CROSS JOIN (VALUES
                    (N'Food', 0),
                    (N'Groceries', 1),
                    (N'Gadgets', 2),
                    (N'Drinks', 3),
                    (N'Pharmacy', 4),
                    (N'Pets', 5)
                ) AS v([Name], [SortOrder])
                WHERE NOT EXISTS (
                    SELECT 1 FROM [OperatorPabiliBrowseCategories] c
                    WHERE c.[OperatorId] = o.[Id] AND c.[Name] = v.[Name]
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorPabiliBrowseCategories");
        }
    }
}
