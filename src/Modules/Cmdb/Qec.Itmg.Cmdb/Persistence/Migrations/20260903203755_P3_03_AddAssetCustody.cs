using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3_03_AddAssetCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Asset",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    WarrantyExpiry = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asset", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asset_ConfigurationItem_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssetAssignment",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetAssignment_Asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "cmdb",
                        principalTable: "Asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Asset_AssetNumber",
                schema: "cmdb",
                table: "Asset",
                column: "AssetNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asset_ConfigurationItemId",
                schema: "cmdb",
                table: "Asset",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetAssignment_AssetId_Active",
                schema: "cmdb",
                table: "AssetAssignment",
                column: "AssetId",
                unique: true,
                filter: "[ReturnedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetAssignment",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "Asset",
                schema: "cmdb");
        }
    }
}
