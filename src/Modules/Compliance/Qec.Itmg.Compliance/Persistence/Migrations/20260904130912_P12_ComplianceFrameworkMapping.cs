using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Compliance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P12_ComplianceFrameworkMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cmp");

            migrationBuilder.CreateTable(
                name: "ComplianceCalendarItem",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FrameworkVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceCalendarItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControlAssessment",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AssessorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssessmentDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TestProcedureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlAssessment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Framework",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Publisher = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Framework", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkVersion",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PublishedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrameworkVersion_Framework_FrameworkId",
                        column: x => x.FrameworkId,
                        principalSchema: "cmp",
                        principalTable: "Framework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkRequirement",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RequirementType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkRequirement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrameworkRequirement_FrameworkRequirement_ParentRequirementId",
                        column: x => x.ParentRequirementId,
                        principalSchema: "cmp",
                        principalTable: "FrameworkRequirement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrameworkRequirement_FrameworkVersion_FrameworkVersionId",
                        column: x => x.FrameworkVersionId,
                        principalSchema: "cmp",
                        principalTable: "FrameworkVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlMapping",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Relationship = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlMapping_FrameworkRequirement_FrameworkRequirementId",
                        column: x => x.FrameworkRequirementId,
                        principalSchema: "cmp",
                        principalTable: "FrameworkRequirement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCalendarItem_DueAt",
                schema: "cmp",
                table: "ComplianceCalendarItem",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCalendarItem_Owner",
                schema: "cmp",
                table: "ComplianceCalendarItem",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCalendarItem_Status",
                schema: "cmp",
                table: "ComplianceCalendarItem",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ControlAssessment_AssessmentDate",
                schema: "cmp",
                table: "ControlAssessment",
                column: "AssessmentDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ControlAssessment_Control_Status",
                schema: "cmp",
                table: "ControlAssessment",
                columns: new[] { "InternalControlId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlAssessment_FrameworkVersion",
                schema: "cmp",
                table: "ControlAssessment",
                column: "FrameworkVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlMapping_Control_Requirement",
                schema: "cmp",
                table: "ControlMapping",
                columns: new[] { "InternalControlId", "FrameworkRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlMapping_FrameworkRequirementId",
                schema: "cmp",
                table: "ControlMapping",
                column: "FrameworkRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Framework_Code",
                schema: "cmp",
                table: "Framework",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRequirement_Parent",
                schema: "cmp",
                table: "FrameworkRequirement",
                column: "ParentRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRequirement_Version_Code",
                schema: "cmp",
                table: "FrameworkRequirement",
                columns: new[] { "FrameworkVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkVersion_Framework_Version",
                schema: "cmp",
                table: "FrameworkVersion",
                columns: new[] { "FrameworkId", "VersionCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceCalendarItem",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "ControlAssessment",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "ControlMapping",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "FrameworkRequirement",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "FrameworkVersion",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "Framework",
                schema: "cmp");
        }
    }
}
