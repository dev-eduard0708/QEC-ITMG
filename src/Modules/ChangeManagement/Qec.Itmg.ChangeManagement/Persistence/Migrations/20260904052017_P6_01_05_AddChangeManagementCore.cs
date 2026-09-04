using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ChangeManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P6_01_05_AddChangeManagementCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chg");

            migrationBuilder.CreateTable(
                name: "ChangeRequest",
                schema: "chg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RiskRating = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TechnicalImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SecurityImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImplementationPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TestPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RollbackPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ScheduledStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ImplementationStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ImplementationCompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ValidationNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PirNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsRetrospective = table.Column<bool>(type: "bit", nullable: false),
                    IsPreAuthorizedStandard = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChangeApproval",
                schema: "chg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeApproval", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeApproval_ChangeRequest_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalSchema: "chg",
                        principalTable: "ChangeRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeConfigurationItem",
                schema: "chg",
                columns: table => new
                {
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeConfigurationItem", x => new { x.ChangeRequestId, x.ConfigurationItemId });
                    table.ForeignKey(
                        name: "FK_ChangeConfigurationItem_ChangeRequest_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalSchema: "chg",
                        principalTable: "ChangeRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeStatusHistory",
                schema: "chg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeStatusHistory_ChangeRequest_ChangeRequestId",
                        column: x => x.ChangeRequestId,
                        principalSchema: "chg",
                        principalTable: "ChangeRequest",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeApproval_Change_Approver_Decision",
                schema: "chg",
                table: "ChangeApproval",
                columns: new[] { "ChangeRequestId", "ApproverUserId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeConfigurationItem_ConfigurationItemId",
                schema: "chg",
                table: "ChangeConfigurationItem",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequest_ChangeNumber",
                schema: "chg",
                table: "ChangeRequest",
                column: "ChangeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequest_OwnerUserId",
                schema: "chg",
                table: "ChangeRequest",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequest_Status_UpdatedAtUtc",
                schema: "chg",
                table: "ChangeRequest",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequest_Type",
                schema: "chg",
                table: "ChangeRequest",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeStatusHistory_Change_ChangedAt",
                schema: "chg",
                table: "ChangeStatusHistory",
                columns: new[] { "ChangeRequestId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeApproval",
                schema: "chg");

            migrationBuilder.DropTable(
                name: "ChangeConfigurationItem",
                schema: "chg");

            migrationBuilder.DropTable(
                name: "ChangeStatusHistory",
                schema: "chg");

            migrationBuilder.DropTable(
                name: "ChangeRequest",
                schema: "chg");
        }
    }
}
