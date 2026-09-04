using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.DocumentManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P10_PolicyDocumentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "doc");

            migrationBuilder.CreateTable(
                name: "ManagedDocument",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DesignatedApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EffectiveDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    RetirementReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedDocument", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentGovernanceLink",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentGovernanceLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentGovernanceLink_ManagedDocument_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalSchema: "doc",
                        principalTable: "ManagedDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentReviewNotificationLog",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ThresholdDays = table.Column<int>(type: "int", nullable: false),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentReviewNotificationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentReviewNotificationLog_ManagedDocument_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalSchema: "doc",
                        principalTable: "ManagedDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersion",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersion_ManagedDocument_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalSchema: "doc",
                        principalTable: "ManagedDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcknowledgement",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcknowledgement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgement_DocumentVersion_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalSchema: "doc",
                        principalTable: "DocumentVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PolicyAcknowledgement_ManagedDocument_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalSchema: "doc",
                        principalTable: "ManagedDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentGovernanceLink_Doc_Kind_Target",
                schema: "doc",
                table: "DocumentGovernanceLink",
                columns: new[] { "ManagedDocumentId", "LinkKind", "TargetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentReviewNotificationLog_Doc_Date_Threshold",
                schema: "doc",
                table: "DocumentReviewNotificationLog",
                columns: new[] { "ManagedDocumentId", "ReviewDateUtc", "ThresholdDays" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersion_Document_Version",
                schema: "doc",
                table: "DocumentVersion",
                columns: new[] { "ManagedDocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocument_Classification",
                schema: "doc",
                table: "ManagedDocument",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocument_DocumentNumber",
                schema: "doc",
                table: "ManagedDocument",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocument_DocumentType",
                schema: "doc",
                table: "ManagedDocument",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocument_OwnerUserId",
                schema: "doc",
                table: "ManagedDocument",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocument_ReviewDate",
                schema: "doc",
                table: "ManagedDocument",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocument_Status",
                schema: "doc",
                table: "ManagedDocument",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgement_Document_User",
                schema: "doc",
                table: "PolicyAcknowledgement",
                columns: new[] { "ManagedDocumentId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcknowledgement_Version_User",
                schema: "doc",
                table: "PolicyAcknowledgement",
                columns: new[] { "DocumentVersionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentGovernanceLink",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "DocumentReviewNotificationLog",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "PolicyAcknowledgement",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "DocumentVersion",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "ManagedDocument",
                schema: "doc");
        }
    }
}
