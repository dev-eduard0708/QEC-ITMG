using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Ai.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P20_AiAssistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai");

            migrationBuilder.CreateTable(
                name: "AiInteraction",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Capability = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToolCallCount = table.Column<int>(type: "int", nullable: false),
                    RedactionCount = table.Column<int>(type: "int", nullable: false),
                    ClassificationContext = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInteraction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiToolInvocation",
                schema: "ai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InteractionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RecordType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiToolInvocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiToolInvocation_AiInteraction_InteractionId",
                        column: x => x.InteractionId,
                        principalSchema: "ai",
                        principalTable: "AiInteraction",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiInteraction_Capability_Status",
                schema: "ai",
                table: "AiInteraction",
                columns: new[] { "Capability", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInteraction_CorrelationId",
                schema: "ai",
                table: "AiInteraction",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiInteraction_UserId_StartedAtUtc",
                schema: "ai",
                table: "AiInteraction",
                columns: new[] { "UserId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiToolInvocation_InteractionId_ToolName",
                schema: "ai",
                table: "AiToolInvocation",
                columns: new[] { "InteractionId", "ToolName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiToolInvocation",
                schema: "ai");

            migrationBuilder.DropTable(
                name: "AiInteraction",
                schema: "ai");
        }
    }
}
