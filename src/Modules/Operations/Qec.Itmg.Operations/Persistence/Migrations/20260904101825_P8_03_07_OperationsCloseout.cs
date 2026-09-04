using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P8_03_07_OperationsCloseout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupJob",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExternalJobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupJob", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRecord",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Issuer = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Thumbprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRecord", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatchBaseline",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatchBaseline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatchDeployment",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatchBaselineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatchDeployment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestoreTest",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BackupJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PerformedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreTest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJob",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExternalJobId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduleDescription = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastResult = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    NextRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJob", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupRun",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BackupJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupRun_BackupJob_BackupJobId",
                        column: x => x.BackupJobId,
                        principalSchema: "ops",
                        principalTable: "BackupJob",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateExpiryNotificationLog",
                schema: "ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThresholdDays = table.Column<int>(type: "int", nullable: false),
                    NotifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateExpiryNotificationLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateExpiryNotificationLog_CertificateRecord_CertificateId",
                        column: x => x.CertificateId,
                        principalSchema: "ops",
                        principalTable: "CertificateRecord",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalEvent_Status_UpdatedAtUtc",
                schema: "ops",
                table: "OperationalEvent",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackupJob_IsActive",
                schema: "ops",
                table: "BackupJob",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BackupJob_Provider_Name",
                schema: "ops",
                table: "BackupJob",
                columns: new[] { "Provider", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_BackupRun_Job_Started",
                schema: "ops",
                table: "BackupRun",
                columns: new[] { "BackupJobId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BackupRun_Status",
                schema: "ops",
                table: "BackupRun",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateExpiryNotificationLog_Cert_Threshold",
                schema: "ops",
                table: "CertificateExpiryNotificationLog",
                columns: new[] { "CertificateId", "ThresholdDays" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRecord_ExpiresAtUtc",
                schema: "ops",
                table: "CertificateRecord",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRecord_IsActive",
                schema: "ops",
                table: "CertificateRecord",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRecord_Thumbprint",
                schema: "ops",
                table: "CertificateRecord",
                column: "Thumbprint");

            migrationBuilder.CreateIndex(
                name: "IX_PatchBaseline_IsActive",
                schema: "ops",
                table: "PatchBaseline",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PatchDeployment_CI_Status",
                schema: "ops",
                table: "PatchDeployment",
                columns: new[] { "ConfigurationItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PatchDeployment_ScheduledAtUtc",
                schema: "ops",
                table: "PatchDeployment",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreTest_Result",
                schema: "ops",
                table: "RestoreTest",
                column: "Result");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreTest_ScheduledAtUtc",
                schema: "ops",
                table: "RestoreTest",
                column: "ScheduledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJob_IsActive",
                schema: "ops",
                table: "ScheduledJob",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJob_NextRunAtUtc",
                schema: "ops",
                table: "ScheduledJob",
                column: "NextRunAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupRun",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "CertificateExpiryNotificationLog",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "PatchBaseline",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "PatchDeployment",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "RestoreTest",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "ScheduledJob",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "BackupJob",
                schema: "ops");

            migrationBuilder.DropTable(
                name: "CertificateRecord",
                schema: "ops");

            migrationBuilder.DropIndex(
                name: "IX_OperationalEvent_Status_UpdatedAtUtc",
                schema: "ops",
                table: "OperationalEvent");
        }
    }
}
