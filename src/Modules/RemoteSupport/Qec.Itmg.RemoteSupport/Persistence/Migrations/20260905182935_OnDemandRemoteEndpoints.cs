using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.RemoteSupport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnDemandRemoteEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ConfigurationItemId",
                schema: "rem",
                table: "RemoteSessionRequest",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "RemoteEndpointId",
                schema: "rem",
                table: "RemoteSessionRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RemoteEndpoint",
                schema: "rem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentRemoteSessionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EngineNodeId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    EndpointKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperatingSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperatingSystemVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Architecture = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    HelperVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    AgentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConnectionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteEndpoint", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemoteEndpointEnrollment",
                schema: "rem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteSessionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RedeemedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedFromIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteEndpointEnrollment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteEndpointEnrollment_RemoteSessionRequest_RemoteSessionRequestId",
                        column: x => x.RemoteSessionRequestId,
                        principalSchema: "rem",
                        principalTable: "RemoteSessionRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionRequest_RemoteEndpointId",
                schema: "rem",
                table: "RemoteSessionRequest",
                column: "RemoteEndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpoint_ConnectionStatus",
                schema: "rem",
                table: "RemoteEndpoint",
                column: "ConnectionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpoint_CurrentSession",
                schema: "rem",
                table: "RemoteEndpoint",
                column: "CurrentRemoteSessionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpoint_ExpiresAtUtc",
                schema: "rem",
                table: "RemoteEndpoint",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpoint_OwnerUserId",
                schema: "rem",
                table: "RemoteEndpoint",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointEnrollment_Session",
                schema: "rem",
                table: "RemoteEndpointEnrollment",
                column: "RemoteSessionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointEnrollment_TokenHash",
                schema: "rem",
                table: "RemoteEndpointEnrollment",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointEnrollment_UserId",
                schema: "rem",
                table: "RemoteEndpointEnrollment",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteEndpoint",
                schema: "rem");

            migrationBuilder.DropTable(
                name: "RemoteEndpointEnrollment",
                schema: "rem");

            migrationBuilder.DropIndex(
                name: "IX_RemoteSessionRequest_RemoteEndpointId",
                schema: "rem",
                table: "RemoteSessionRequest");

            migrationBuilder.DropColumn(
                name: "RemoteEndpointId",
                schema: "rem",
                table: "RemoteSessionRequest");

            migrationBuilder.AlterColumn<Guid>(
                name: "ConfigurationItemId",
                schema: "rem",
                table: "RemoteSessionRequest",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
