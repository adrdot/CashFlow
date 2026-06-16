using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Reporting.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySummariesAndOptimizeProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedProjections");

            migrationBuilder.CreateTable(
                name: "DailySummaries",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalDebits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCredits = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DebitEntryCount = table.Column<int>(type: "int", nullable: false),
                    CreditEntryCount = table.Column<int>(type: "int", nullable: false),
                    TransactionVolume = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailySummaries", x => new { x.UserId, x.ReportDate });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectedTransactions_UserId_OccurredOn",
                table: "ProjectedTransactions",
                columns: new[] { "UserId", "OccurredOn" });

            migrationBuilder.Sql("""
                INSERT INTO DailySummaries (UserId, ReportDate, TotalDebits, TotalCredits, DebitEntryCount, CreditEntryCount, TransactionVolume, LastUpdatedUtc)
                SELECT
                    UserId,
                    OccurredOn,
                    SUM(CASE WHEN Type = 1 THEN Amount ELSE 0 END),
                    SUM(CASE WHEN Type = 2 THEN Amount ELSE 0 END),
                    SUM(CASE WHEN Type = 1 THEN 1 ELSE 0 END),
                    SUM(CASE WHEN Type = 2 THEN 1 ELSE 0 END),
                    COUNT(*),
                    SYSUTCDATETIME()
                FROM ProjectedTransactions
                GROUP BY UserId, OccurredOn
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailySummaries");

            migrationBuilder.DropIndex(
                name: "IX_ProjectedTransactions_UserId_OccurredOn",
                table: "ProjectedTransactions");

            migrationBuilder.CreateTable(
                name: "ProcessedProjections",
                columns: table => new
                {
                    TransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedProjections", x => x.TransactionId);
                });
        }
    }
}
