using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P2_05_AddWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowDefinition",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinition", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowState",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowState", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowState_WorkflowDefinition_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "plt",
                        principalTable: "WorkflowDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTransition",
                schema: "plt",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredPermission = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RequiresReason = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTransition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTransition_WorkflowDefinition_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalSchema: "plt",
                        principalTable: "WorkflowDefinition",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_WorkflowDefinition_Key_Version",
                schema: "plt",
                table: "WorkflowDefinition",
                columns: new[] { "Key", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_WorkflowState_Definition_Key",
                schema: "plt",
                table: "WorkflowState",
                columns: new[] { "WorkflowDefinitionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_WorkflowTransition_From_To",
                schema: "plt",
                table: "WorkflowTransition",
                columns: new[] { "WorkflowDefinitionId", "FromStateId", "ToStateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowState",
                schema: "plt");

            migrationBuilder.DropTable(
                name: "WorkflowTransition",
                schema: "plt");

            migrationBuilder.DropTable(
                name: "WorkflowDefinition",
                schema: "plt");
        }
    }
}
