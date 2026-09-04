using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ServiceDesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P5_07_AddKnownError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsKnownError",
                schema: "sd",
                table: "Problem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "KnownErrorAtUtc",
                schema: "sd",
                table: "Problem",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KnownErrorByUserId",
                schema: "sd",
                table: "Problem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Problem_IsKnownError",
                schema: "sd",
                table: "Problem",
                column: "IsKnownError");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Problem_IsKnownError",
                schema: "sd",
                table: "Problem");

            migrationBuilder.DropColumn(
                name: "IsKnownError",
                schema: "sd",
                table: "Problem");

            migrationBuilder.DropColumn(
                name: "KnownErrorAtUtc",
                schema: "sd",
                table: "Problem");

            migrationBuilder.DropColumn(
                name: "KnownErrorByUserId",
                schema: "sd",
                table: "Problem");
        }
    }
}
