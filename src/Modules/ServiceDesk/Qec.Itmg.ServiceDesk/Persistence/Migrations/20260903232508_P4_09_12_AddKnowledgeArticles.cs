using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ServiceDesk.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P4_09_12_AddKnowledgeArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeArticle",
                schema: "sd",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticle", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticle_Slug",
                schema: "sd",
                table: "KnowledgeArticle",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeArticle_Status_UpdatedAtUtc",
                schema: "sd",
                table: "KnowledgeArticle",
                columns: new[] { "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeArticle",
                schema: "sd");
        }
    }
}
