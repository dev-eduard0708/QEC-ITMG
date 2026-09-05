using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.RemoteSupport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P7_RemoteSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rem");

            migrationBuilder.CreateTable(
                name: "RemoteSessionRequest",
                schema: "rem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TechnicianUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedPrivileges = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    SessionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AllowedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeclinedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConnectingAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EngineSessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EngineJoinUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ConsentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsentIpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ElevationUsed = table.Column<bool>(type: "bit", nullable: true),
                    RecordingReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LastEngineError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MfaSatisfiedAtStart = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSessionRequest", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_ActiveLookup",
                schema: "rem",
                table: "RemoteSessionRequest",
                columns: new[] { "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_ChangeRequestId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_ConfigurationItemId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_EngineSessionId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "EngineSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_RemoteNumber",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "RemoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_RequestedAtUtc",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "RequestedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_Status",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_TargetUserId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_TechnicianUserId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_TicketId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteSessionRequest",
                schema: "rem");
        }
    }
}
