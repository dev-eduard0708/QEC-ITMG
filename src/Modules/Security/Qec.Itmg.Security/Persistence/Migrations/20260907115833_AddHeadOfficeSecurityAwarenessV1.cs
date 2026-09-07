using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Security.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadOfficeSecurityAwarenessV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                schema: "sec",
                table: "AwarenessCampaign",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Description",
                schema: "sec",
                table: "AwarenessCampaign",
                newName: "DescriptionEn");

            migrationBuilder.Sql(
                """
                UPDATE [sec].[AwarenessCampaign]
                SET [Status] = N'Active'
                WHERE [Status] = N'Open';
                """);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentSource",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignVersionId",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PassedAtUtc",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotDepartmentName",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotDisplayName",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotPositionNames",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotUpn",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDepartmentId",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePositionId",
                schema: "sec",
                table: "AwarenessCompletion",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRetry",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Number",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedVersionId",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireCompletion",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireQuiz",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "TitleAr",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                schema: "sec",
                table: "AwarenessCampaign",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                """
                UPDATE [sec].[AwarenessCampaign]
                SET [UpdatedAtUtc] = [CreatedAtUtc]
                WHERE [UpdatedAtUtc] < '2000-01-01';
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SubmittedAtUtc",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "Passed",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignVersionId",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                """
                UPDATE [sec].[AwarenessAttempt]
                SET [StartedAtUtc] = [SubmittedAtUtc]
                WHERE [SubmittedAtUtc] IS NOT NULL AND [StartedAtUtc] < '2000-01-01';
                """);

            migrationBuilder.CreateTable(
                name: "AwarenessAudienceRule",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessAudienceRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessAudienceRule_AwarenessCampaign_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "sec",
                        principalTable: "AwarenessCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessCampaignQuestion",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QuestionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    QuestionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExplanationEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExplanationAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessCampaignQuestion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessCampaignVersion",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequireQuiz = table.Column<bool>(type: "bit", nullable: false),
                    PassingScorePercent = table.Column<int>(type: "int", nullable: false),
                    AllowRetry = table.Column<bool>(type: "bit", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessCampaignVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessCampaignVersion_AwarenessCampaign_CampaignId",
                        column: x => x.CampaignId,
                        principalSchema: "sec",
                        principalTable: "AwarenessCampaign",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessContentBlock",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    BodyEn = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    BodyAr = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessContentBlock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessQuizAnswer",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedOptionIdsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessQuizAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessQuizAnswer_AwarenessAttempt_AttemptId",
                        column: x => x.AttemptId,
                        principalSchema: "sec",
                        principalTable: "AwarenessAttempt",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AwarenessCampaignQuestionOption",
                schema: "sec",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TextEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TextAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AwarenessCampaignQuestionOption", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AwarenessCampaignQuestionOption_AwarenessCampaignQuestion_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "sec",
                        principalTable: "AwarenessCampaignQuestion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCompletion_Version_User",
                schema: "sec",
                table: "AwarenessCompletion",
                columns: new[] { "CampaignVersionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaign_Number",
                schema: "sec",
                table: "AwarenessCampaign",
                column: "Number",
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaign_PublishedVersionId",
                schema: "sec",
                table: "AwarenessCampaign",
                column: "PublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessAttempt_CampaignVersionId",
                schema: "sec",
                table: "AwarenessAttempt",
                column: "CampaignVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessAudienceRule_CampaignId",
                schema: "sec",
                table: "AwarenessAudienceRule",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaignQuestion_Campaign_Order",
                schema: "sec",
                table: "AwarenessCampaignQuestion",
                columns: new[] { "CampaignId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaignQuestion_Version_Order",
                schema: "sec",
                table: "AwarenessCampaignQuestion",
                columns: new[] { "CampaignVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaignQuestionOption_Order",
                schema: "sec",
                table: "AwarenessCampaignQuestionOption",
                columns: new[] { "QuestionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessCampaignVersion_Campaign_Number",
                schema: "sec",
                table: "AwarenessCampaignVersion",
                columns: new[] { "CampaignId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessContentBlock_Campaign_Order",
                schema: "sec",
                table: "AwarenessContentBlock",
                columns: new[] { "CampaignId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessContentBlock_Version_Order",
                schema: "sec",
                table: "AwarenessContentBlock",
                columns: new[] { "CampaignVersionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AwarenessQuizAnswer_Attempt_Question",
                schema: "sec",
                table: "AwarenessQuizAnswer",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AwarenessAudienceRule",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessCampaignQuestionOption",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessCampaignVersion",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessContentBlock",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessQuizAnswer",
                schema: "sec");

            migrationBuilder.DropTable(
                name: "AwarenessCampaignQuestion",
                schema: "sec");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCompletion_Version_User",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCampaign_Number",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessCampaign_PublishedVersionId",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropIndex(
                name: "IX_AwarenessAttempt_CampaignVersionId",
                schema: "sec",
                table: "AwarenessAttempt");

            migrationBuilder.DropColumn(
                name: "AssignmentSource",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "CampaignVersionId",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "PassedAtUtc",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "SnapshotDepartmentName",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "SnapshotDisplayName",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "SnapshotPositionNames",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "SnapshotUpn",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "SourceDepartmentId",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "SourcePositionId",
                schema: "sec",
                table: "AwarenessCompletion");

            migrationBuilder.DropColumn(
                name: "AllowRetry",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "Number",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "PublishedVersionId",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "RequireCompletion",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "RequireQuiz",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "TitleAr",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                schema: "sec",
                table: "AwarenessCampaign");

            migrationBuilder.DropColumn(
                name: "CampaignVersionId",
                schema: "sec",
                table: "AwarenessAttempt");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                schema: "sec",
                table: "AwarenessAttempt");

            migrationBuilder.Sql(
                """
                UPDATE [sec].[AwarenessCampaign]
                SET [Status] = N'Open'
                WHERE [Status] = N'Active';
                """);

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                schema: "sec",
                table: "AwarenessCampaign",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "DescriptionEn",
                schema: "sec",
                table: "AwarenessCampaign",
                newName: "Description");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SubmittedAtUtc",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Score",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Passed",
                schema: "sec",
                table: "AwarenessAttempt",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);
        }
    }
}
