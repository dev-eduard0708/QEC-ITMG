using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.DocumentManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyWorkflowResponsibilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PublisherUserId",
                schema: "doc",
                table: "ManagedDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                schema: "doc",
                table: "ManagedDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedByUserId",
                schema: "doc",
                table: "DocumentVersion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAtUtc",
                schema: "doc",
                table: "DocumentVersion",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                schema: "doc",
                table: "DocumentVersion",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublisherUserId",
                schema: "doc",
                table: "ManagedDocument");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                schema: "doc",
                table: "ManagedDocument");

            migrationBuilder.DropColumn(
                name: "PublishedByUserId",
                schema: "doc",
                table: "DocumentVersion");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                schema: "doc",
                table: "DocumentVersion");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                schema: "doc",
                table: "DocumentVersion");
        }
    }
}
