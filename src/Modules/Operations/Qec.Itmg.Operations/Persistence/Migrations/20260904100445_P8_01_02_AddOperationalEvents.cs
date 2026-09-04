using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P8_01_02_AddOperationalEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ops");

            migrationBuilder.CreateTable(
                name: "OperationalEvent",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceEventKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurrenceCount = table.Column<int>(type: "int", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvent_ConfigurationItemId",
                schema: "ops",
                table: "OperationalEvent",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvent_EventNumber",
                schema: "ops",
                table: "OperationalEvent",
                column: "EventNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvent_Source_SourceEventKey",
                schema: "ops",
                table: "OperationalEvent",
                columns: new[] { "Source", "SourceEventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvent_Status_LastSeenAtUtc",
                schema: "ops",
                table: "OperationalEvent",
                columns: new[] { "Status", "LastSeenAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalEvent",
                schema: "ops");
        }
    }
}
