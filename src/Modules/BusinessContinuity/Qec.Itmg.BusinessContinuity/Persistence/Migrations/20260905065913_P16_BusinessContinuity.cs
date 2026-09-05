using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.BusinessContinuity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P16_BusinessContinuity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bcm");

            migrationBuilder.CreateTable(
                name: "BiaRecord",
                schema: "bcm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BiaNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessProcessName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BusinessImpactSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    FinancialImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OperationalImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RegulatoryImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReputationalImpact = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaximumTolerableDowntimeMinutes = table.Column<int>(type: "int", nullable: true),
                    Criticality = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiaRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContinuityNotificationLog",
                schema: "bcm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContinuityNotificationLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContinuityPlan",
                schema: "bcm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContinuityPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContinuityScopeLink",
                schema: "bcm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContinuityScopeLink", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrTest",
                schema: "bcm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DrTestNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContinuityPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PlannedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ObservedRtoMinutes = table.Column<int>(type: "int", nullable: true),
                    ObservedRpoMinutes = table.Column<int>(type: "int", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    Gaps = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrTest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecoveryProcedure",
                schema: "bcm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProcedureNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContinuityPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: true),
                    RecoveryStage = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryProcedure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecoveryProcedure_ContinuityPlan_ContinuityPlanId",
                        column: x => x.ContinuityPlanId,
                        principalSchema: "bcm",
                        principalTable: "ContinuityPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BiaRecord_Number",
                schema: "bcm",
                table: "BiaRecord",
                column: "BiaNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BiaRecord_Service_Status",
                schema: "bcm",
                table: "BiaRecord",
                columns: new[] { "BusinessServiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContinuityNotificationLog_Unique",
                schema: "bcm",
                table: "ContinuityNotificationLog",
                columns: new[] { "ResourceId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContinuityPlan_Number",
                schema: "bcm",
                table: "ContinuityPlan",
                column: "PlanNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContinuityPlan_ReviewAtUtc",
                schema: "bcm",
                table: "ContinuityPlan",
                column: "ReviewAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ContinuityPlan_Type_Status",
                schema: "bcm",
                table: "ContinuityPlan",
                columns: new[] { "PlanType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContinuityScopeLink_Unique",
                schema: "bcm",
                table: "ContinuityScopeLink",
                columns: new[] { "OwnerId", "OwnerType", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrTest_Number",
                schema: "bcm",
                table: "DrTest",
                column: "DrTestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrTest_PlannedAtUtc",
                schema: "bcm",
                table: "DrTest",
                column: "PlannedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DrTest_Service_Status",
                schema: "bcm",
                table: "DrTest",
                columns: new[] { "BusinessServiceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryProcedure_Number",
                schema: "bcm",
                table: "RecoveryProcedure",
                column: "ProcedureNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryProcedure_Plan_Sequence",
                schema: "bcm",
                table: "RecoveryProcedure",
                columns: new[] { "ContinuityPlanId", "Sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BiaRecord",
                schema: "bcm");

            migrationBuilder.DropTable(
                name: "ContinuityNotificationLog",
                schema: "bcm");

            migrationBuilder.DropTable(
                name: "ContinuityScopeLink",
                schema: "bcm");

            migrationBuilder.DropTable(
                name: "DrTest",
                schema: "bcm");

            migrationBuilder.DropTable(
                name: "RecoveryProcedure",
                schema: "bcm");

            migrationBuilder.DropTable(
                name: "ContinuityPlan",
                schema: "bcm");
        }
    }
}
