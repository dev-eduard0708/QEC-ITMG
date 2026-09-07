using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.AccessManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessCaseReworkAndRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentScopeRevisionNumber",
                schema: "acc",
                table: "AccessCase",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAtUtc",
                schema: "acc",
                table: "AccessCase",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReturnedForReworkAtUtc",
                schema: "acc",
                table: "AccessCase",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnedForReworkByUserId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReworkReason",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessCaseRevision",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCaseRevision", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCaseRevision_AccessCase_AccessCaseId",
                        column: x => x.AccessCaseId,
                        principalSchema: "acc",
                        principalTable: "AccessCase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessCaseRevisionItem",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessEntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntitlementKeySnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEnSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameArSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CustomName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsPrivileged = table.Column<bool>(type: "bit", nullable: false),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCaseRevisionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCaseRevisionItem_AccessCaseRevision_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "acc",
                        principalTable: "AccessCaseRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessCaseRevisionItem_AccessEntitlement_AccessEntitlementId",
                        column: x => x.AccessEntitlementId,
                        principalSchema: "acc",
                        principalTable: "AccessEntitlement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseRevision_Case_Decision",
                schema: "acc",
                table: "AccessCaseRevision",
                columns: new[] { "AccessCaseId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseRevision_Case_RevisionNumber",
                schema: "acc",
                table: "AccessCaseRevision",
                columns: new[] { "AccessCaseId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseRevisionItem_AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseRevisionItem",
                column: "AccessEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseRevisionItem_RevisionId",
                schema: "acc",
                table: "AccessCaseRevisionItem",
                column: "RevisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessCaseRevisionItem",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessCaseRevision",
                schema: "acc");

            migrationBuilder.DropColumn(
                name: "CurrentScopeRevisionNumber",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "ReturnedForReworkAtUtc",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "ReturnedForReworkByUserId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "ReworkReason",
                schema: "acc",
                table: "AccessCase");
        }
    }
}
