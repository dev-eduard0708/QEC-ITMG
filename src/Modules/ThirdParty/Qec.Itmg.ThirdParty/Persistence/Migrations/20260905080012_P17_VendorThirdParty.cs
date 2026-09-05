using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.ThirdParty.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P17_VendorThirdParty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tpm");

            migrationBuilder.CreateTable(
                name: "Vendor",
                schema: "tpm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Criticality = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ServiceDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    PrimaryContactPhone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorNotificationLog",
                schema: "tpm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorNotificationLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contract",
                schema: "tpm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContractType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RenewalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AutoRenew = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SlaReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contract", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contract_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "tpm",
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorAssessment",
                schema: "tpm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    RiskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorAssessment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorAssessment_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "tpm",
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorContact",
                schema: "tpm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorContact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorContact_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "tpm",
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorScopeLink",
                schema: "tpm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorScopeLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorScopeLink_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalSchema: "tpm",
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contract_EndDate",
                schema: "tpm",
                table: "Contract",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Number",
                schema: "tpm",
                table: "Contract",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contract_RenewalDate",
                schema: "tpm",
                table: "Contract",
                column: "RenewalDate");

            migrationBuilder.CreateIndex(
                name: "IX_Contract_Vendor_Status",
                schema: "tpm",
                table: "Contract",
                columns: new[] { "VendorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_Criticality",
                schema: "tpm",
                table: "Vendor",
                column: "Criticality");

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_Name_Status",
                schema: "tpm",
                table: "Vendor",
                columns: new[] { "Name", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendor_Number",
                schema: "tpm",
                table: "Vendor",
                column: "VendorNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorAssessment_DueAtUtc",
                schema: "tpm",
                table: "VendorAssessment",
                column: "DueAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VendorAssessment_Number",
                schema: "tpm",
                table: "VendorAssessment",
                column: "AssessmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorAssessment_Vendor_Status",
                schema: "tpm",
                table: "VendorAssessment",
                columns: new[] { "VendorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorContact_Vendor_Email",
                schema: "tpm",
                table: "VendorContact",
                columns: new[] { "VendorId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorNotificationLog_Unique",
                schema: "tpm",
                table: "VendorNotificationLog",
                columns: new[] { "ResourceId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorScopeLink_Unique",
                schema: "tpm",
                table: "VendorScopeLink",
                columns: new[] { "VendorId", "TargetType", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contract",
                schema: "tpm");

            migrationBuilder.DropTable(
                name: "VendorAssessment",
                schema: "tpm");

            migrationBuilder.DropTable(
                name: "VendorContact",
                schema: "tpm");

            migrationBuilder.DropTable(
                name: "VendorNotificationLog",
                schema: "tpm");

            migrationBuilder.DropTable(
                name: "VendorScopeLink",
                schema: "tpm");

            migrationBuilder.DropTable(
                name: "Vendor",
                schema: "tpm");
        }
    }
}
