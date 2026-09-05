using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.RemoteSupport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteEndpointPairing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteEndpointPairing",
                schema: "rem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceSecretHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AuthorizedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AuthorizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedFromIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteEndpointPairing", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointPairing_DeviceSecretHash",
                schema: "rem",
                table: "RemoteEndpointPairing",
                column: "DeviceSecretHash");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointPairing_ExpiresAtUtc",
                schema: "rem",
                table: "RemoteEndpointPairing",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointPairing_Status",
                schema: "rem",
                table: "RemoteEndpointPairing",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteEndpointPairing_UserCode",
                schema: "rem",
                table: "RemoteEndpointPairing",
                column: "UserCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteEndpointPairing",
                schema: "rem");
        }
    }
}
