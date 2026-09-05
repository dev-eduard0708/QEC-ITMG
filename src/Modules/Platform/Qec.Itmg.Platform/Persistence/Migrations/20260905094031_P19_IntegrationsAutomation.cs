using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P19_IntegrationsAutomation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationCorrelation",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MatchStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationCorrelation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationRun",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProcessedCount = table.Column<int>(type: "int", nullable: false),
                    SucceededCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    UnmatchedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationWebhookReceipt",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalEventId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationWebhookReceipt", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCorrelation_MatchStatus_Provider",
                schema: "plt",
                table: "IntegrationCorrelation",
                columns: new[] { "MatchStatus", "Provider" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationCorrelation_Provider_ExternalId_TargetType",
                schema: "plt",
                table: "IntegrationCorrelation",
                columns: new[] { "Provider", "ExternalId", "TargetType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationRun_CorrelationId",
                schema: "plt",
                table: "IntegrationRun",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationRun_Provider_StartedAtUtc",
                schema: "plt",
                table: "IntegrationRun",
                columns: new[] { "Provider", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationWebhookReceipt_Provider_ExternalEventId",
                schema: "plt",
                table: "IntegrationWebhookReceipt",
                columns: new[] { "Provider", "ExternalEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationWebhookReceipt_ReceivedAtUtc",
                schema: "plt",
                table: "IntegrationWebhookReceipt",
                column: "ReceivedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationCorrelation",
                schema: "plt");

            migrationBuilder.DropTable(
                name: "IntegrationRun",
                schema: "plt");

            migrationBuilder.DropTable(
                name: "IntegrationWebhookReceipt",
                schema: "plt");
        }
    }
}
