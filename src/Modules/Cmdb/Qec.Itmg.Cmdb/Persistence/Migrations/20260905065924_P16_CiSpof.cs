using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P16_CiSpof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSinglePointOfFailure",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SpofMitigationNotes",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpofReason",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SpofReviewedAtUtc",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SpofRiskId",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItem_IsSinglePointOfFailure",
                schema: "cmdb",
                table: "ConfigurationItem",
                column: "IsSinglePointOfFailure");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItem_IsSinglePointOfFailure",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "IsSinglePointOfFailure",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "SpofMitigationNotes",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "SpofReason",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "SpofReviewedAtUtc",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "SpofRiskId",
                schema: "cmdb",
                table: "ConfigurationItem");
        }
    }
}
