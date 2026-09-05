using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Reporting.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P18_AdvancedReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rpt");

            migrationBuilder.CreateTable(
                name: "ReportSnapshot",
                schema: "rpt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SnapshotDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportSnapshot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshot_Date",
                schema: "rpt",
                table: "ReportSnapshot",
                column: "SnapshotDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshot_Key_Date",
                schema: "rpt",
                table: "ReportSnapshot",
                columns: new[] { "SnapshotKey", "SnapshotDateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportSnapshot_PeriodStart",
                schema: "rpt",
                table: "ReportSnapshot",
                column: "PeriodStartUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportSnapshot",
                schema: "rpt");
        }
    }
}
