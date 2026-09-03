using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P4_05_08_AddAttachmentResourceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                schema: "plt",
                table: "AttachmentMetadata",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceType",
                schema: "plt",
                table: "AttachmentMetadata",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentMetadata_ResourceType_ResourceId",
                schema: "plt",
                table: "AttachmentMetadata",
                columns: new[] { "ResourceType", "ResourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttachmentMetadata_ResourceType_ResourceId",
                schema: "plt",
                table: "AttachmentMetadata");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                schema: "plt",
                table: "AttachmentMetadata");

            migrationBuilder.DropColumn(
                name: "ResourceType",
                schema: "plt",
                table: "AttachmentMetadata");
        }
    }
}
