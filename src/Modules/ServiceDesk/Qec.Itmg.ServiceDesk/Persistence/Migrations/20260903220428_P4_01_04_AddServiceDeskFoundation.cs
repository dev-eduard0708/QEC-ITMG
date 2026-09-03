using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ServiceDesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P4_01_04_AddServiceDeskFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sd");

            migrationBuilder.CreateTable(
                name: "SlaPolicy",
                schema: "sd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TicketType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ResponseMinutes = table.Column<int>(type: "int", nullable: false),
                    ResolutionMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaPolicy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportQueue",
                schema: "sd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ticket",
                schema: "sd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QueueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SlaPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolutionDueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResponseBreached = table.Column<bool>(type: "bit", nullable: false),
                    ResolutionBreached = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ticket_SlaPolicy_SlaPolicyId",
                        column: x => x.SlaPolicyId,
                        principalSchema: "sd",
                        principalTable: "SlaPolicy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Ticket_SupportQueue_QueueId",
                        column: x => x.QueueId,
                        principalSchema: "sd",
                        principalTable: "SupportQueue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TicketAssignmentHistory",
                schema: "sd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QueueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAssignmentHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentHistory_Ticket_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "sd",
                        principalTable: "Ticket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SlaPolicy_Priority_TicketType_IsActive",
                schema: "sd",
                table: "SlaPolicy",
                columns: new[] { "Priority", "TicketType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportQueue_Name",
                schema: "sd",
                table: "SupportQueue",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_QueueId",
                schema: "sd",
                table: "Ticket",
                column: "QueueId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_RequesterUserId",
                schema: "sd",
                table: "Ticket",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_SlaPolicyId",
                schema: "sd",
                table: "Ticket",
                column: "SlaPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Status_UpdatedAtUtc",
                schema: "sd",
                table: "Ticket",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_TicketNumber",
                schema: "sd",
                table: "Ticket",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentHistory_TicketId_AssignedAtUtc",
                schema: "sd",
                table: "TicketAssignmentHistory",
                columns: new[] { "TicketId", "AssignedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAssignmentHistory",
                schema: "sd");

            migrationBuilder.DropTable(
                name: "Ticket",
                schema: "sd");

            migrationBuilder.DropTable(
                name: "SlaPolicy",
                schema: "sd");

            migrationBuilder.DropTable(
                name: "SupportQueue",
                schema: "sd");
        }
    }
}
