using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P3_02_AddCiRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CiRelationship",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetCiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CiRelationship", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CiRelationship_ConfigurationItem_SourceCiId",
                        column: x => x.SourceCiId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CiRelationship_ConfigurationItem_TargetCiId",
                        column: x => x.TargetCiId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CiRelationship_Source_Target_Type",
                schema: "cmdb",
                table: "CiRelationship",
                columns: new[] { "SourceCiId", "TargetCiId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CiRelationship_TargetCiId",
                schema: "cmdb",
                table: "CiRelationship",
                column: "TargetCiId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CiRelationship",
                schema: "cmdb");
        }
    }
}
