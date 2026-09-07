using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Organization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentMembershipAndGoogleProfileImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "org",
                table: "Department",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                schema: "org",
                table: "Department",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                schema: "org",
                table: "Department",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentDepartmentId",
                schema: "org",
                table: "Department",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "org",
                table: "Department",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill Code from Name for existing rows (idempotent uppercase + non-alnum → _).
            migrationBuilder.Sql("""
                UPDATE org.Department
                SET Code = UPPER(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(Name)), ' ', '_'), '-', '_'), '/', '_'), '.', '_'))
                WHERE Code IS NULL OR LTRIM(RTRIM(Code)) = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "org",
                table: "Department",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Department_Code",
                schema: "org",
                table: "Department",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Department_ParentDepartmentId",
                schema: "org",
                table: "Department",
                column: "ParentDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Department_Department_ParentDepartmentId",
                schema: "org",
                table: "Department",
                column: "ParentDepartmentId",
                principalSchema: "org",
                principalTable: "Department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateTable(
                name: "DepartmentMembership",
                schema: "org",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentMembership", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentMembership_Department_DepartmentId",
                        column: x => x.DepartmentId,
                        principalSchema: "org",
                        principalTable: "Department",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentMembership_UserId",
                schema: "org",
                table: "DepartmentMembership",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentMembership_Department_User_Active",
                schema: "org",
                table: "DepartmentMembership",
                columns: new[] { "DepartmentId", "UserId" },
                unique: true,
                filter: "[EffectiveTo] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentMembership",
                schema: "org");

            migrationBuilder.DropForeignKey(
                name: "FK_Department_Department_ParentDepartmentId",
                schema: "org",
                table: "Department");

            migrationBuilder.DropIndex(
                name: "IX_Department_Code",
                schema: "org",
                table: "Department");

            migrationBuilder.DropIndex(
                name: "IX_Department_ParentDepartmentId",
                schema: "org",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "org",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "NameAr",
                schema: "org",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                schema: "org",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "ParentDepartmentId",
                schema: "org",
                table: "Department");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "org",
                table: "Department");
        }
    }
}
