using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Audit.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P14_AuditManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "aud");

            migrationBuilder.CreateTable(
                name: "AuditEngagement",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AuditType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ScopeSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    LeadAuditorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEngagement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditQuestion",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FrameworkRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditQuestion_AuditEngagement_AuditEngagementId",
                        column: x => x.AuditEngagementId,
                        principalSchema: "aud",
                        principalTable: "AuditEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditScopeLink",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditScopeLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditScopeLink_AuditEngagement_AuditEngagementId",
                        column: x => x.AuditEngagementId,
                        principalSchema: "aud",
                        principalTable: "AuditEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceRequest",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestedFromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FulfilledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceRequest_AuditEngagement_AuditEngagementId",
                        column: x => x.AuditEngagementId,
                        principalSchema: "aud",
                        principalTable: "AuditEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Finding",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FindingNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AuditEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedRiskReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExceptionReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Finding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Finding_AuditEngagement_AuditEngagementId",
                        column: x => x.AuditEngagementId,
                        principalSchema: "aud",
                        principalTable: "AuditEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceRequestNotificationLog",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceRequestNotificationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceRequestNotificationLog_EvidenceRequest_EvidenceRequestId",
                        column: x => x.EvidenceRequestId,
                        principalSchema: "aud",
                        principalTable: "EvidenceRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveAction",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VerificationNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveAction_Finding_FindingId",
                        column: x => x.FindingId,
                        principalSchema: "aud",
                        principalTable: "Finding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManagementResponse",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseText = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ManagementOwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagementResponse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagementResponse_Finding_FindingId",
                        column: x => x.FindingId,
                        principalSchema: "aud",
                        principalTable: "Finding",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEngagement_AuditNumber",
                schema: "aud",
                table: "AuditEngagement",
                column: "AuditNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEngagement_EndDate",
                schema: "aud",
                table: "AuditEngagement",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEngagement_StartDate",
                schema: "aud",
                table: "AuditEngagement",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEngagement_Status",
                schema: "aud",
                table: "AuditEngagement",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditQuestion_Engagement_Sort",
                schema: "aud",
                table: "AuditQuestion",
                columns: new[] { "AuditEngagementId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditScopeLink_Engagement_Target",
                schema: "aud",
                table: "AuditScopeLink",
                columns: new[] { "AuditEngagementId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveAction_ActionNumber",
                schema: "aud",
                table: "CorrectiveAction",
                column: "ActionNumber",
                unique: true,
                filter: "[ActionNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveAction_DueAtUtc",
                schema: "aud",
                table: "CorrectiveAction",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveAction_Finding_Status",
                schema: "aud",
                table: "CorrectiveAction",
                columns: new[] { "FindingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequest_DueAtUtc",
                schema: "aud",
                table: "EvidenceRequest",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequest_Engagement_Status",
                schema: "aud",
                table: "EvidenceRequest",
                columns: new[] { "AuditEngagementId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequestNotificationLog_Request_Event",
                schema: "aud",
                table: "EvidenceRequestNotificationLog",
                columns: new[] { "EvidenceRequestId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finding_Engagement_Status",
                schema: "aud",
                table: "Finding",
                columns: new[] { "AuditEngagementId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Finding_FindingNumber",
                schema: "aud",
                table: "Finding",
                column: "FindingNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Finding_Severity",
                schema: "aud",
                table: "Finding",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ManagementResponse_FindingId",
                schema: "aud",
                table: "ManagementResponse",
                column: "FindingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditQuestion",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "AuditScopeLink",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "CorrectiveAction",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "EvidenceRequestNotificationLog",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "ManagementResponse",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "EvidenceRequest",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "Finding",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "AuditEngagement",
                schema: "aud");
        }
    }
}
