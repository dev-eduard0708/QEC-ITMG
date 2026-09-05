using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.AccessManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P17_AccessVendorLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                schema: "acc",
                table: "ManagedAccount",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                schema: "acc",
                table: "AccessCase",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManagedAccount_VendorId",
                schema: "acc",
                table: "ManagedAccount",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessCase_VendorId",
                schema: "acc",
                table: "AccessCase",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ManagedAccount_VendorId",
                schema: "acc",
                table: "ManagedAccount");

            migrationBuilder.DropIndex(
                name: "IX_AccessCase_VendorId",
                schema: "acc",
                table: "AccessCase");

            migrationBuilder.DropColumn(
                name: "VendorId",
                schema: "acc",
                table: "ManagedAccount");

            migrationBuilder.DropColumn(
                name: "VendorId",
                schema: "acc",
                table: "AccessCase");
        }
    }
}
