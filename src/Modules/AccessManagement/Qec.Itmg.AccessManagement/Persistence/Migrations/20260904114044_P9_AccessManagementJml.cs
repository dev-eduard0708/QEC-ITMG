using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.AccessManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P9_AccessManagementJml : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "acc");

            migrationBuilder.CreateTable(
                name: "AccessCase",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SubjectEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DesignatedApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkedTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExistingAccessConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ExistingAccessConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExistingAccessConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCase", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccessReviewCampaign",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessReviewCampaign", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagedAccount",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedAccount", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SodRule",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApplicationConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeftEntitlementKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RightEntitlementKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SodRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccessCaseException",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AuthorizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatedSodRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCaseException", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCaseException_AccessCase_AccessCaseId",
                        column: x => x.AccessCaseId,
                        principalSchema: "acc",
                        principalTable: "AccessCase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessCaseItem",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntitlementKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsPrivileged = table.Column<bool>(type: "bit", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FulfilledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FulfilledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCaseItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCaseItem_AccessCase_AccessCaseId",
                        column: x => x.AccessCaseId,
                        principalSchema: "acc",
                        principalTable: "AccessCase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExistingAccessSnapshotItem",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntitlementKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AccessSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExistingAccessSnapshotItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExistingAccessSnapshotItem_AccessCase_AccessCaseId",
                        column: x => x.AccessCaseId,
                        principalSchema: "acc",
                        principalTable: "AccessCase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessReviewItem",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccessSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReviewerComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessReviewItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessReviewItem_AccessReviewCampaign_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "acc",
                        principalTable: "AccessReviewCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_CaseNumber",
                schema: "acc",
                table: "AccessCase",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_EffectiveAtUtc",
                schema: "acc",
                table: "AccessCase",
                column: "EffectiveAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_RequesterUserId",
                schema: "acc",
                table: "AccessCase",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_Status_Type",
                schema: "acc",
                table: "AccessCase",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_SubjectUserId",
                schema: "acc",
                table: "AccessCase",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseException_Case_Type",
                schema: "acc",
                table: "AccessCaseException",
                columns: new[] { "AccessCaseId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseItem_Case_Status",
                schema: "acc",
                table: "AccessCaseItem",
                columns: new[] { "AccessCaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseItem_EntitlementKey",
                schema: "acc",
                table: "AccessCaseItem",
                column: "EntitlementKey");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviewCampaign_Reviewer",
                schema: "acc",
                table: "AccessReviewCampaign",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviewCampaign_Status_Due",
                schema: "acc",
                table: "AccessReviewCampaign",
                columns: new[] { "Status", "DueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviewItem_Campaign_Decision",
                schema: "acc",
                table: "AccessReviewItem",
                columns: new[] { "CampaignId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_ExistingAccessSnapshotItem_Case",
                schema: "acc",
                table: "ExistingAccessSnapshotItem",
                column: "AccessCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedAccount_AccountName",
                schema: "acc",
                table: "ManagedAccount",
                column: "AccountName");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedAccount_OwnerUserId",
                schema: "acc",
                table: "ManagedAccount",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedAccount_Type_Status",
                schema: "acc",
                table: "ManagedAccount",
                columns: new[] { "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SodRule_IsActive",
                schema: "acc",
                table: "SodRule",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SodRule_Left_Right",
                schema: "acc",
                table: "SodRule",
                columns: new[] { "LeftEntitlementKey", "RightEntitlementKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessCaseException",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessCaseItem",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessReviewItem",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "ExistingAccessSnapshotItem",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "ManagedAccount",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "SodRule",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessReviewCampaign",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessCase",
                schema: "acc");
        }
    }
}
