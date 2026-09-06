using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.AccessManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessCategoryRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccessCategoryId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessCategoryKeySnapshot",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessCategoryNameSnapshot",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAtUtc",
                schema: "acc",
                table: "AccessCase",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FallbackReason",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreferSubjectEmployeeVerificationSnapshot",
                schema: "acc",
                table: "AccessCase",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerificationComment",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationMethod",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationOutcome",
                schema: "acc",
                table: "AccessCase",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAtUtc",
                schema: "acc",
                table: "AccessCase",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessCaseRouteParticipant",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsSubjectEmployeeDerived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCaseRouteParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCaseRouteParticipant_AccessCase_AccessCaseId",
                        column: x => x.AccessCaseId,
                        principalSchema: "acc",
                        principalTable: "AccessCase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccessCategory",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PreferSubjectEmployeeVerification = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccessCategoryParticipant",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCategoryParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCategoryParticipant_AccessCategory_AccessCategoryId",
                        column: x => x.AccessCategoryId,
                        principalSchema: "acc",
                        principalTable: "AccessCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_AccessCategoryId",
                schema: "acc",
                table: "AccessCase",
                column: "AccessCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseRouteParticipant_Case_Stage_User",
                schema: "acc",
                table: "AccessCaseRouteParticipant",
                columns: new[] { "AccessCaseId", "Stage", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseRouteParticipant_User_Stage",
                schema: "acc",
                table: "AccessCaseRouteParticipant",
                columns: new[] { "UserId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCategory_IsActive",
                schema: "acc",
                table: "AccessCategory",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCategory_Key",
                schema: "acc",
                table: "AccessCategory",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCategoryParticipant_Category_Stage_User",
                schema: "acc",
                table: "AccessCategoryParticipant",
                columns: new[] { "AccessCategoryId", "Stage", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessCaseRouteParticipant",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessCategoryParticipant",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessCategory",
                schema: "acc");

            migrationBuilder.DropIndex(
                name: "IX_AccessCase_AccessCategoryId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "AccessCategoryId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "AccessCategoryKeySnapshot",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "AccessCategoryNameSnapshot",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "FallbackReason",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "PreferSubjectEmployeeVerificationSnapshot",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "VerificationComment",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "VerificationMethod",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "VerificationOutcome",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                schema: "acc",
                table: "AccessCase");
        }
    }
}
