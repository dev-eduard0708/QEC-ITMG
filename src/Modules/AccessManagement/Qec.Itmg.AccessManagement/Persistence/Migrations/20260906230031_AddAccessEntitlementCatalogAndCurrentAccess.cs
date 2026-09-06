using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.AccessManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessEntitlementCatalogAndCurrentAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntitlementNameArSnapshot",
                schema: "acc",
                table: "AccessCaseItem",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntitlementNameEnSnapshot",
                schema: "acc",
                table: "AccessCaseItem",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCustom",
                schema: "acc",
                table: "AccessCaseItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AccessEntitlement",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DefaultRevokeAction = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsPrivileged = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessEntitlement", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccessCategoryEntitlement",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessEntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefaultForJoiner = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessCategoryEntitlement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessCategoryEntitlement_AccessCategory_AccessCategoryId",
                        column: x => x.AccessCategoryId,
                        principalSchema: "acc",
                        principalTable: "AccessCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccessCategoryEntitlement_AccessEntitlement_AccessEntitlementId",
                        column: x => x.AccessEntitlementId,
                        principalSchema: "acc",
                        principalTable: "AccessEntitlement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAccessEntitlement",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessEntitlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntitlementKeySnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EntitlementNameSnapshot = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    GrantedFromAccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastChangedFromAccessCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccessEntitlement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccessEntitlement_AccessEntitlement_AccessEntitlementId",
                        column: x => x.AccessEntitlementId,
                        principalSchema: "acc",
                        principalTable: "AccessEntitlement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessCaseItem_AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseItem",
                column: "AccessEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCategoryEntitlement_AccessEntitlementId",
                schema: "acc",
                table: "AccessCategoryEntitlement",
                column: "AccessEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCategoryEntitlement_Category_Entitlement",
                schema: "acc",
                table: "AccessCategoryEntitlement",
                columns: new[] { "AccessCategoryId", "AccessEntitlementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccessCategoryEntitlement_Category_Sort",
                schema: "acc",
                table: "AccessCategoryEntitlement",
                columns: new[] { "AccessCategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessEntitlement_IsActive",
                schema: "acc",
                table: "AccessEntitlement",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AccessEntitlement_Key",
                schema: "acc",
                table: "AccessEntitlement",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessEntitlement_AccessEntitlementId",
                schema: "acc",
                table: "UserAccessEntitlement",
                column: "AccessEntitlementId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessEntitlement_User_Entitlement_Active",
                schema: "acc",
                table: "UserAccessEntitlement",
                columns: new[] { "UserId", "AccessEntitlementId" },
                unique: true,
                filter: "[AccessEntitlementId] IS NOT NULL AND [Status] = N'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessEntitlement_User_Key_Active",
                schema: "acc",
                table: "UserAccessEntitlement",
                columns: new[] { "UserId", "EntitlementKeySnapshot" },
                unique: true,
                filter: "[AccessEntitlementId] IS NULL AND [Status] = N'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccessEntitlement_User_Status",
                schema: "acc",
                table: "UserAccessEntitlement",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccessCaseItem_AccessEntitlement_AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseItem",
                column: "AccessEntitlementId",
                principalSchema: "acc",
                principalTable: "AccessEntitlement",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccessCaseItem_AccessEntitlement_AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseItem");

            migrationBuilder.DropTable(
                name: "AccessCategoryEntitlement",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "UserAccessEntitlement",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "AccessEntitlement",
                schema: "acc");

            migrationBuilder.DropIndex(
                name: "IX_AccessCaseItem_AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseItem");

            migrationBuilder.DropColumn(
                name: "AccessEntitlementId",
                schema: "acc",
                table: "AccessCaseItem");

            migrationBuilder.DropColumn(
                name: "EntitlementNameArSnapshot",
                schema: "acc",
                table: "AccessCaseItem");

            migrationBuilder.DropColumn(
                name: "EntitlementNameEnSnapshot",
                schema: "acc",
                table: "AccessCaseItem");

            migrationBuilder.DropColumn(
                name: "IsCustom",
                schema: "acc",
                table: "AccessCaseItem");
        }
    }
}
