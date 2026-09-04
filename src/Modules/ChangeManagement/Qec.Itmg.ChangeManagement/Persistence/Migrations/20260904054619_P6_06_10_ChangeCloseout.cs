using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ChangeManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P6_06_10_ChangeCloseout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActualImplementationAtUtc",
                schema: "chg",
                table: "ChangeRequest",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                schema: "chg",
                table: "ChangeRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetrospectiveReason",
                schema: "chg",
                table: "ChangeRequest",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetrospectiveRecordedAtUtc",
                schema: "chg",
                table: "ChangeRequest",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StandardChangeCatalogItem",
                schema: "chg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RiskRating = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ImplementationPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TestPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RollbackPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardChangeCatalogItem", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequest_CatalogItemId",
                schema: "chg",
                table: "ChangeRequest",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequest_IsRetrospective",
                schema: "chg",
                table: "ChangeRequest",
                column: "IsRetrospective");

            migrationBuilder.CreateIndex(
                name: "IX_StandardChangeCatalogItem_Code",
                schema: "chg",
                table: "StandardChangeCatalogItem",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandardChangeCatalogItem_IsActive",
                schema: "chg",
                table: "StandardChangeCatalogItem",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandardChangeCatalogItem",
                schema: "chg");

            migrationBuilder.DropIndex(
                name: "IX_ChangeRequest_CatalogItemId",
                schema: "chg",
                table: "ChangeRequest");

            migrationBuilder.DropIndex(
                name: "IX_ChangeRequest_IsRetrospective",
                schema: "chg",
                table: "ChangeRequest");

            migrationBuilder.DropColumn(
                name: "ActualImplementationAtUtc",
                schema: "chg",
                table: "ChangeRequest");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                schema: "chg",
                table: "ChangeRequest");

            migrationBuilder.DropColumn(
                name: "RetrospectiveReason",
                schema: "chg",
                table: "ChangeRequest");

            migrationBuilder.DropColumn(
                name: "RetrospectiveRecordedAtUtc",
                schema: "chg",
                table: "ChangeRequest");
        }
    }
}
