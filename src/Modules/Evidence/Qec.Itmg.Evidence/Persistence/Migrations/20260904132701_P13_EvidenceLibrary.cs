using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Evidence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P13_EvidenceLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "evd");

            migrationBuilder.CreateTable(
                name: "Evidence",
                schema: "evd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvidenceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Classification = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ValidTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceExpiryNotificationLog",
                schema: "evd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ThresholdDays = table.Column<int>(type: "int", nullable: false),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceExpiryNotificationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceExpiryNotificationLog_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalSchema: "evd",
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceLink",
                schema: "evd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceLink_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalSchema: "evd",
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceVersion",
                schema: "evd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SupersedesVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceVersion_Evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalSchema: "evd",
                        principalTable: "Evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_Classification",
                schema: "evd",
                table: "Evidence",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_EvidenceNumber",
                schema: "evd",
                table: "Evidence",
                column: "EvidenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_OwnerUserId",
                schema: "evd",
                table: "Evidence",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_Source",
                schema: "evd",
                table: "Evidence",
                columns: new[] { "SourceType", "SourceRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_Status",
                schema: "evd",
                table: "Evidence",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_ValidTo",
                schema: "evd",
                table: "Evidence",
                column: "ValidTo");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceExpiryNotificationLog_Evidence_Date_Threshold",
                schema: "evd",
                table: "EvidenceExpiryNotificationLog",
                columns: new[] { "EvidenceId", "ValidToUtc", "ThresholdDays" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLink_Evidence_Target",
                schema: "evd",
                table: "EvidenceLink",
                columns: new[] { "EvidenceId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceLink_Target",
                schema: "evd",
                table: "EvidenceLink",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceVersion_Evidence_Version",
                schema: "evd",
                table: "EvidenceVersion",
                columns: new[] { "EvidenceId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvidenceExpiryNotificationLog",
                schema: "evd");

            migrationBuilder.DropTable(
                name: "EvidenceLink",
                schema: "evd");

            migrationBuilder.DropTable(
                name: "EvidenceVersion",
                schema: "evd");

            migrationBuilder.DropTable(
                name: "Evidence",
                schema: "evd");
        }
    }
}
