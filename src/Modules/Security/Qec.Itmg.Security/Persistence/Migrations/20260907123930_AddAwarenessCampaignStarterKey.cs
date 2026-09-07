using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Security.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAwarenessCampaignStarterKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AddColumn<string>(
                name: "StarterKey",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaign_StarterKey",
                schema: "sec",
                table: "AwarenessCampaign",
                column: "StarterKey",
                unique: true,
                filter: "[StarterKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AwarenessCampaign_StarterKey",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "StarterKey",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);
        }
    }
}
