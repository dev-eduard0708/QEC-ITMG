using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qec.Itmg.DocumentManagement.Persistence;

#nullable disable

namespace Qec.Itmg.DocumentManagement.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DocumentManagementDbContext))]
    [Migration("20260906120000_AddDocumentLocalization")]
    public partial class AddDocumentLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedLanguage",
                schema: "doc",
                table: "PolicyAcknowledgement",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManagedDocumentTranslation",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedDocumentTranslation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagedDocumentTranslation_ManagedDocument_ManagedDocumentId",
                        column: x => x.ManagedDocumentId,
                        principalSchema: "doc",
                        principalTable: "ManagedDocument",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVersionTranslation",
                schema: "doc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    ContentText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersionTranslation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentVersionTranslation_DocumentVersion_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalSchema: "doc",
                        principalTable: "DocumentVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManagedDocumentTranslation_Document_Language",
                schema: "doc",
                table: "ManagedDocumentTranslation",
                columns: new[] { "ManagedDocumentId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersionTranslation_Version_Language",
                schema: "doc",
                table: "DocumentVersionTranslation",
                columns: new[] { "DocumentVersionId", "LanguageCode" },
                unique: true);

            // Backfill English translations from legacy columns (idempotent-safe insert).
            migrationBuilder.Sql("""
                INSERT INTO doc.ManagedDocumentTranslation (Id, ManagedDocumentId, LanguageCode, Title, CreatedAtUtc, UpdatedAtUtc)
                SELECT NEWID(), d.Id, N'en', d.Title, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM doc.ManagedDocument d
                WHERE NOT EXISTS (
                    SELECT 1 FROM doc.ManagedDocumentTranslation t
                    WHERE t.ManagedDocumentId = d.Id AND t.LanguageCode = N'en');
                """);

            migrationBuilder.Sql("""
                INSERT INTO doc.DocumentVersionTranslation (Id, DocumentVersionId, LanguageCode, ContentText, ChangeSummary, CreatedAtUtc, UpdatedAtUtc)
                SELECT NEWID(), v.Id, N'en', v.ContentText, v.ChangeSummary, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM doc.DocumentVersion v
                WHERE v.ContentText IS NOT NULL AND LTRIM(RTRIM(v.ContentText)) <> N''
                  AND NOT EXISTS (
                    SELECT 1 FROM doc.DocumentVersionTranslation t
                    WHERE t.DocumentVersionId = v.Id AND t.LanguageCode = N'en');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentVersionTranslation",
                schema: "doc");

            migrationBuilder.DropTable(
                name: "ManagedDocumentTranslation",
                schema: "doc");

            migrationBuilder.DropColumn(
                name: "AcknowledgedLanguage",
                schema: "doc",
                table: "PolicyAcknowledgement");
        }
    }
}
