using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Compliance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceReadinessTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileType",
                schema: "cmp",
                table: "Framework",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Generic");

            migrationBuilder.Sql(
                """
                UPDATE cmp.Framework SET ProfileType = N'Generic' WHERE ProfileType = N'' OR ProfileType IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "FrameworkRequirementApplicability",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SetByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkRequirementApplicability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrameworkRequirementApplicability_FrameworkRequirement_FrameworkRequirementId",
                        column: x => x.FrameworkRequirementId,
                        principalSchema: "cmp",
                        principalTable: "FrameworkRequirement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkRequirementOperationalLink",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InternalRoute = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkRequirementOperationalLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrameworkRequirementOperationalLink_FrameworkRequirement_FrameworkRequirementId",
                        column: x => x.FrameworkRequirementId,
                        principalSchema: "cmp",
                        principalTable: "FrameworkRequirement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkRequirementTranslation",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkRequirementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkRequirementTranslation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrameworkRequirementTranslation_FrameworkRequirement_FrameworkRequirementId",
                        column: x => x.FrameworkRequirementId,
                        principalSchema: "cmp",
                        principalTable: "FrameworkRequirement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkTranslation",
                schema: "cmp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkTranslation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FrameworkTranslation_Framework_FrameworkId",
                        column: x => x.FrameworkId,
                        principalSchema: "cmp",
                        principalTable: "Framework",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Framework_ProfileType",
                schema: "cmp",
                table: "Framework",
                column: "ProfileType");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRequirementApplicability_Requirement",
                schema: "cmp",
                table: "FrameworkRequirementApplicability",
                column: "FrameworkRequirementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRequirementOperationalLink_Requirement",
                schema: "cmp",
                table: "FrameworkRequirementOperationalLink",
                column: "FrameworkRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRequirementTranslation_Requirement_Language",
                schema: "cmp",
                table: "FrameworkRequirementTranslation",
                columns: new[] { "FrameworkRequirementId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkTranslation_Framework_Language",
                schema: "cmp",
                table: "FrameworkTranslation",
                columns: new[] { "FrameworkId", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FrameworkRequirementApplicability",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "FrameworkRequirementOperationalLink",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "FrameworkRequirementTranslation",
                schema: "cmp");

            migrationBuilder.DropTable(
                name: "FrameworkTranslation",
                schema: "cmp");

            migrationBuilder.DropIndex(
                name: "IX_Framework_ProfileType",
                schema: "cmp",
                table: "Framework");

            migrationBuilder.DropColumn(
                name: "ProfileType",
                schema: "cmp",
                table: "Framework");
        }
    }
}
