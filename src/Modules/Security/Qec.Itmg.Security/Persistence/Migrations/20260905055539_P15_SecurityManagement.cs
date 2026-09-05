using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Security.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P15_SecurityManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sec");

            migrationBuilder.CreateTable(
                name: "AwarenessCampaign",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessCampaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PenetrationTest",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PentestNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ScopeSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReportEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PenetrationTest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyException",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExceptionNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompensatingControls = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyException", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Risk",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Likelihood = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    InherentScore = table.Column<int>(type: "int", nullable: false),
                    ResidualLikelihood = table.Column<int>(type: "int", nullable: true),
                    ResidualImpact = table.Column<int>(type: "int", nullable: true),
                    ResidualScore = table.Column<int>(type: "int", nullable: true),
                    Treatment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TreatmentPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Risk", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vulnerability",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VulnerabilityNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DetectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AcceptedRiskReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerability", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessCompletion",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessCompletion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessCompletion_AwarenessCampaign_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "sec",
                        principalTable: "AwarenessCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PentestFinding",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PenetrationTestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VulnerabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuditFindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PentestFinding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PentestFinding_PenetrationTest_PenetrationTestId",
                        column: x => x.PenetrationTestId,
                        principalSchema: "sec",
                        principalTable: "PenetrationTest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExceptionExpiryNotificationLog",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExceptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionExpiryNotificationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExceptionExpiryNotificationLog_PolicyException_ExceptionId",
                        column: x => x.ExceptionId,
                        principalSchema: "sec",
                        principalTable: "PolicyException",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskLink",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskLink_Risk_RiskId",
                        column: x => x.RiskId,
                        principalSchema: "sec",
                        principalTable: "Risk",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VulnerabilityRemediationLink",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VulnerabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VulnerabilityRemediationLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VulnerabilityRemediationLink_Vulnerability_VulnerabilityId",
                        column: x => x.VulnerabilityId,
                        principalSchema: "sec",
                        principalTable: "Vulnerability",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaign_DueAtUtc",
                schema: "sec",
                table: "AwarenessCampaign",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaign_Status",
                schema: "sec",
                table: "AwarenessCampaign",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCompletion_Campaign_User",
                schema: "sec",
                table: "AwarenessCompletion",
                columns: new[] { "CampaignId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionExpiryNotificationLog_Unique",
                schema: "sec",
                table: "ExceptionExpiryNotificationLog",
                columns: new[] { "ExceptionId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PenetrationTest_Number",
                schema: "sec",
                table: "PenetrationTest",
                column: "PentestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PenetrationTest_StartDate",
                schema: "sec",
                table: "PenetrationTest",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_PenetrationTest_Status",
                schema: "sec",
                table: "PenetrationTest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PentestFinding_Test_Status",
                schema: "sec",
                table: "PentestFinding",
                columns: new[] { "PenetrationTestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyException_ExpiresAtUtc",
                schema: "sec",
                table: "PolicyException",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyException_Number",
                schema: "sec",
                table: "PolicyException",
                column: "ExceptionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyException_Status",
                schema: "sec",
                table: "PolicyException",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_InherentScore",
                schema: "sec",
                table: "Risk",
                column: "InherentScore");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_Number",
                schema: "sec",
                table: "Risk",
                column: "RiskNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Risk_Owner",
                schema: "sec",
                table: "Risk",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_ResidualScore",
                schema: "sec",
                table: "Risk",
                column: "ResidualScore");

            migrationBuilder.CreateIndex(
                name: "IX_Risk_Status",
                schema: "sec",
                table: "Risk",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLink_Unique",
                schema: "sec",
                table: "RiskLink",
                columns: new[] { "RiskId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerability_CI",
                schema: "sec",
                table: "Vulnerability",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerability_DueAtUtc",
                schema: "sec",
                table: "Vulnerability",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerability_Number",
                schema: "sec",
                table: "Vulnerability",
                column: "VulnerabilityNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerability_Severity",
                schema: "sec",
                table: "Vulnerability",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerability_Status",
                schema: "sec",
                table: "Vulnerability",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VulnerabilityRemediationLink_Unique",
                schema: "sec",
                table: "VulnerabilityRemediationLink",
                columns: new[] { "VulnerabilityId", "LinkType", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwarenessCompletion",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "ExceptionExpiryNotificationLog",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "PentestFinding",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "RiskLink",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "VulnerabilityRemediationLink",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessCampaign",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "PolicyException",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "PenetrationTest",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Risk",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "Vulnerability",
                schema: "sec");
        }
    }
}
