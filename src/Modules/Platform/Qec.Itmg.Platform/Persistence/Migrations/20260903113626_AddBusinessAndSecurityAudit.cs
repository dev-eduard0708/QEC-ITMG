using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessAndSecurityAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "plt");

            migrationBuilder.CreateTable(
                name: "BusinessAuditRecord",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAuditRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditEvent",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TargetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditEvent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditRecord_ActorUserId",
                schema: "plt",
                table: "BusinessAuditRecord",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditRecord_Aggregate",
                schema: "plt",
                table: "BusinessAuditRecord",
                columns: new[] { "AggregateType", "AggregateId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditRecord_CorrelationId",
                schema: "plt",
                table: "BusinessAuditRecord",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAuditRecord_OccurredAtUtc",
                schema: "plt",
                table: "BusinessAuditRecord",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvent_ActorUserId",
                schema: "plt",
                table: "SecurityAuditEvent",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvent_CorrelationId",
                schema: "plt",
                table: "SecurityAuditEvent",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvent_EventType_OccurredAtUtc",
                schema: "plt",
                table: "SecurityAuditEvent",
                columns: new[] { "EventType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvent_OccurredAtUtc",
                schema: "plt",
                table: "SecurityAuditEvent",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessAuditRecord",
                schema: "plt");

            migrationBuilder.DropTable(
                name: "SecurityAuditEvent",
                schema: "plt");
        }
    }
}
