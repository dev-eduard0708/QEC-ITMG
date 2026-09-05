using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Security.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAwarenessEmployeeWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssignedAtUtc",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAtUtc",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModuleVersion",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModuleVersion",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PassThresholdPercent",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AwarenessAttempt",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Passed = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessAttempt", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessAttempt_AwarenessCompletion_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "sec",
                        principalTable: "AwarenessCompletion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessModule",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    PassThresholdPercent = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessModule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessReminderLog",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessReminderLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessReminderLog_AwarenessCompletion_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "sec",
                        principalTable: "AwarenessCompletion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessQuestion",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionText = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessQuestion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessQuestion_AwarenessModule_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "sec",
                        principalTable: "AwarenessModule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessAnswerOption",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessAnswerOption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessAnswerOption_AwarenessQuestion_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "sec",
                        principalTable: "AwarenessQuestion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCompletion_DueAtUtc",
                schema: "sec",
                table: "AwarenessCompletion",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCompletion_Status",
                schema: "sec",
                table: "AwarenessCompletion",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCompletion_UserId",
                schema: "sec",
                table: "AwarenessCompletion",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaign_ModuleId",
                schema: "sec",
                table: "AwarenessCampaign",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessAnswer_Question_Order",
                schema: "sec",
                table: "AwarenessAnswerOption",
                columns: new[] { "QuestionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessAttempt_Assignment_Number",
                schema: "sec",
                table: "AwarenessAttempt",
                columns: new[] { "AssignmentId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessAttempt_AssignmentId",
                schema: "sec",
                table: "AwarenessAttempt",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessModule_Code",
                schema: "sec",
                table: "AwarenessModule",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessModule_Status",
                schema: "sec",
                table: "AwarenessModule",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessQuestion_Module_Order",
                schema: "sec",
                table: "AwarenessQuestion",
                columns: new[] { "ModuleId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessReminder_Assignment_Kind",
                schema: "sec",
                table: "AwarenessReminderLog",
                columns: new[] { "AssignmentId", "ReminderKind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwarenessAnswerOption",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessAttempt",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessReminderLog",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessQuestion",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessModule",
                schema: "sec");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCompletion_DueAtUtc",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCompletion_Status",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCompletion_UserId",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCampaign_ModuleId",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "AssignedAtUtc",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "DueAtUtc",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "ModuleVersion",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "Score",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "ModuleVersion",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "PassThresholdPercent",
                schema: "sec",
                table: "AwarenessCampaign");
        }
    }
}
