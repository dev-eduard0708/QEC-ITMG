using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.RemoteSupport.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemoteSessionChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemoteSessionMessage",
                schema: "rem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemoteSessionRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SystemEventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteSessionMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteSessionMessage_RemoteSessionRequest_RemoteSessionRequestId",
                        column: x => x.RemoteSessionRequestId,
                        principalSchema: "rem",
                        principalTable: "RemoteSessionRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteSessionMessage_Session_Sent",
                schema: "rem",
                table: "RemoteSessionMessage",
                columns: new[] { "RemoteSessionRequestId", "SentAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemoteSessionMessage",
                schema: "rem");
        }
    }
}
