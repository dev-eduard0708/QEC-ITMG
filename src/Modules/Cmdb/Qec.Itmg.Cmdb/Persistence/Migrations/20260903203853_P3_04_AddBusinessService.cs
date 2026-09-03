using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3_04_AddBusinessService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessService",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Criticality = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RtoMinutes = table.Column<int>(type: "int", nullable: true),
                    RpoMinutes = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessService", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessServiceConfigurationItem",
                schema: "cmdb",
                columns: table => new
                {
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessServiceConfigurationItem", x => new { x.BusinessServiceId, x.ConfigurationItemId });
                    table.ForeignKey(
                        name: "FK_BusinessServiceConfigurationItem_BusinessService_BusinessServiceId",
                        column: x => x.BusinessServiceId,
                        principalSchema: "cmdb",
                        principalTable: "BusinessService",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessServiceConfigurationItem_ConfigurationItem_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessService_Name",
                schema: "cmdb",
                table: "BusinessService",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessServiceConfigurationItem_ConfigurationItemId",
                schema: "cmdb",
                table: "BusinessServiceConfigurationItem",
                column: "ConfigurationItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessServiceConfigurationItem",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "BusinessService",
                schema: "cmdb");
        }
    }
}
