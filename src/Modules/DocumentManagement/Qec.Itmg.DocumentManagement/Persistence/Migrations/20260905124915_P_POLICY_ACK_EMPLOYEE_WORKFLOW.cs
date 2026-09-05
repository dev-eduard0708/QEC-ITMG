using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.DocumentManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P_POLICY_ACK_EMPLOYEE_WORKFLOW : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementStatementVersion",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgementText",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssignedAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientIp",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PolicyAssignmentId",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyNumberSnapshot",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyTitleSnapshot",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "RequireReAcknowledgement",
                schema: "doc",
                table: "ManagedDocument",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContentText",
                schema: "doc",
                table: "DocumentVersion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PolicyAssignment",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentScope = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAssignment_DocumentVersion_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalSchema: "doc",
                        principalTable: "DocumentVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyAssignment_ManagedDocument_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalSchema: "doc",
                        principalTable: "ManagedDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgementReminderLog",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgementReminderLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgementReminderLog_PolicyAssignment_PolicyAssignmentId",
                        column: x => x.PolicyAssignmentId,
                        principalSchema: "doc",
                        principalTable: "PolicyAssignment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgement_AcknowledgedAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement",
                column: "AcknowledgedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgement_PolicyAssignmentId",
                schema: "doc",
                table: "PolicyAcknowledgement",
                column: "PolicyAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAckReminder_Assignment_User_Kind",
                schema: "doc",
                table: "PolicyAcknowledgementReminderLog",
                columns: new[] { "PolicyAssignmentId", "UserId", "ReminderKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAssignment_Doc_Version_Scope_User",
                schema: "doc",
                table: "PolicyAssignment",
                columns: new[] { "ManagedDocumentId", "DocumentVersionId", "AssignmentScope", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAssignment_DocumentVersionId",
                schema: "doc",
                table: "PolicyAssignment",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAssignment_DueAtUtc",
                schema: "doc",
                table: "PolicyAssignment",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAssignment_User_Version",
                schema: "doc",
                table: "PolicyAssignment",
                columns: new[] { "UserId", "DocumentVersionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PolicyAcknowledgement_PolicyAssignment_PolicyAssignmentId",
                schema: "doc",
                table: "PolicyAcknowledgement",
                column: "PolicyAssignmentId",
                principalSchema: "doc",
                principalTable: "PolicyAssignment",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PolicyAcknowledgement_PolicyAssignment_PolicyAssignmentId",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropTable(
                name: "PolicyAcknowledgementReminderLog",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "PolicyAssignment",
                schema: "doc");

            migrationBuilder.DropIndex(
                name: "IX_PolicyAcknowledgement_AcknowledgedAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropIndex(
                name: "IX_PolicyAcknowledgement_PolicyAssignmentId",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "AcknowledgementStatementVersion",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "AcknowledgementText",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "ClientIp",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "DueAtUtc",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "PolicyAssignmentId",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "PolicyNumberSnapshot",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "PolicyTitleSnapshot",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                schema: "doc",
                table: "PolicyAcknowledgement");

            migrationBuilder.DropColumn(
                name: "RequireReAcknowledgement",
                schema: "doc",
                table: "ManagedDocument");

            migrationBuilder.DropColumn(
                name: "ContentText",
                schema: "doc",
                table: "DocumentVersion");
        }
    }
}
