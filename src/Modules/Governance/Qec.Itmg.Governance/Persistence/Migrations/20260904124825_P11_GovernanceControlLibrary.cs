using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Governance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P11_GovernanceControlLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gov");

            migrationBuilder.CreateTable(
                name: "InternalControl",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControlNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Objective = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AutomationType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PrimaryOwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrimaryOwnerRoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalControl", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationalUnit",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationalUnit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationalUnit_OrganizationalUnit_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "gov",
                        principalTable: "OrganizationalUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationProfile",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ClassificationScheme = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ControlBusinessServiceLink",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlBusinessServiceLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlBusinessServiceLink_InternalControl_InternalControlId",
                        column: x => x.InternalControlId,
                        principalSchema: "gov",
                        principalTable: "InternalControl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlConfigurationItemLink",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlConfigurationItemLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlConfigurationItemLink_InternalControl_InternalControlId",
                        column: x => x.InternalControlId,
                        principalSchema: "gov",
                        principalTable: "InternalControl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlManagedDocumentLink",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagedDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlManagedDocumentLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlManagedDocumentLink_InternalControl_InternalControlId",
                        column: x => x.InternalControlId,
                        principalSchema: "gov",
                        principalTable: "InternalControl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlSecondaryOwner",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlSecondaryOwner", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlSecondaryOwner_InternalControl_InternalControlId",
                        column: x => x.InternalControlId,
                        principalSchema: "gov",
                        principalTable: "InternalControl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlTestProcedure",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ProcedureSteps = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ExpectedResult = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SampleGuidance = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlTestProcedure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlTestProcedure_InternalControl_InternalControlId",
                        column: x => x.InternalControlId,
                        principalSchema: "gov",
                        principalTable: "InternalControl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvidenceRequirement",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InternalControlId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RetentionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvidenceRequirement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvidenceRequirement_InternalControl_InternalControlId",
                        column: x => x.InternalControlId,
                        principalSchema: "gov",
                        principalTable: "InternalControl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationalUnitMembership",
                schema: "gov",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationalUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationalUnitMembership", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationalUnitMembership_OrganizationalUnit_OrganizationalUnitId",
                        column: x => x.OrganizationalUnitId,
                        principalSchema: "gov",
                        principalTable: "OrganizationalUnit",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlServiceLink_Control_Service",
                schema: "gov",
                table: "ControlBusinessServiceLink",
                columns: new[] { "InternalControlId", "BusinessServiceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlCiLink_Control_Ci",
                schema: "gov",
                table: "ControlConfigurationItemLink",
                columns: new[] { "InternalControlId", "ConfigurationItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlDocumentLink_Control_Doc",
                schema: "gov",
                table: "ControlManagedDocumentLink",
                columns: new[] { "InternalControlId", "ManagedDocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlSecondaryOwner_Control_User",
                schema: "gov",
                table: "ControlSecondaryOwner",
                columns: new[] { "InternalControlId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlTestProcedure_InternalControlId",
                schema: "gov",
                table: "ControlTestProcedure",
                column: "InternalControlId");

            migrationBuilder.CreateIndex(
                name: "IX_EvidenceRequirement_InternalControlId",
                schema: "gov",
                table: "EvidenceRequirement",
                column: "InternalControlId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalControl_ControlNumber",
                schema: "gov",
                table: "InternalControl",
                column: "ControlNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalControl_Domain",
                schema: "gov",
                table: "InternalControl",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_InternalControl_PrimaryOwnerUserId",
                schema: "gov",
                table: "InternalControl",
                column: "PrimaryOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalControl_Status",
                schema: "gov",
                table: "InternalControl",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnit_Code",
                schema: "gov",
                table: "OrganizationalUnit",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnit_ManagerUserId",
                schema: "gov",
                table: "OrganizationalUnit",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnit_ParentId",
                schema: "gov",
                table: "OrganizationalUnit",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationalUnitMembership_Unit_User",
                schema: "gov",
                table: "OrganizationalUnitMembership",
                columns: new[] { "OrganizationalUnitId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlBusinessServiceLink",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "ControlConfigurationItemLink",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "ControlManagedDocumentLink",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "ControlSecondaryOwner",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "ControlTestProcedure",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "EvidenceRequirement",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "OrganizationalUnitMembership",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "OrganizationProfile",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "InternalControl",
                schema: "gov");

            migrationBuilder.DropTable(
                name: "OrganizationalUnit",
                schema: "gov");
        }
    }
}
