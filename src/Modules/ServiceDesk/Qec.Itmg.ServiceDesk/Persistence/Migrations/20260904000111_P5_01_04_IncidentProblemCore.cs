using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ServiceDesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P5_01_04_IncidentProblemCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMajorIncident",
                schema: "sd",
                table: "Ticket",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SecurityClassification",
                schema: "sd",
                table: "Ticket",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                schema: "sd",
                table: "Ticket",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Problem",
                schema: "sd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProblemNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Workaround = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProblemIncident",
                schema: "sd",
                columns: table => new
                {
                    ProblemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncidentTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemIncident", x => new { x.ProblemId, x.IncidentTicketId });
                    table.ForeignKey(
                        name: "FK_ProblemIncident_Problem_ProblemId",
                        column: x => x.ProblemId,
                        principalSchema: "sd",
                        principalTable: "Problem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProblemIncident_Ticket_IncidentTicketId",
                        column: x => x.IncidentTicketId,
                        principalSchema: "sd",
                        principalTable: "Ticket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_SourceEventId",
                schema: "sd",
                table: "Ticket",
                column: "SourceEventId",
                unique: true,
                filter: "[SourceEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Problem_ProblemNumber",
                schema: "sd",
                table: "Problem",
                column: "ProblemNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Problem_Status_UpdatedAtUtc",
                schema: "sd",
                table: "Problem",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemIncident_IncidentTicketId",
                schema: "sd",
                table: "ProblemIncident",
                column: "IncidentTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemIncident",
                schema: "sd");

            migrationBuilder.DropTable(
                name: "Problem",
                schema: "sd");

            migrationBuilder.DropIndex(
                name: "IX_Ticket_SourceEventId",
                schema: "sd",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "IsMajorIncident",
                schema: "sd",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "SecurityClassification",
                schema: "sd",
                table: "Ticket");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                schema: "sd",
                table: "Ticket");
        }
    }
}
