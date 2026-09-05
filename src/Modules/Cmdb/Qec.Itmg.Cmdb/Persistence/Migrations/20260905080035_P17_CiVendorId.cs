using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P17_CiVendorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItem_VendorId",
                schema: "cmdb",
                table: "ConfigurationItem",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItem_VendorId",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "VendorId",
                schema: "cmdb",
                table: "ConfigurationItem");
        }
    }
}
