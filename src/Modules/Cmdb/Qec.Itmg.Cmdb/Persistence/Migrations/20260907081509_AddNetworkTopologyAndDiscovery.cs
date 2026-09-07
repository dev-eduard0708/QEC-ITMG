using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkTopologyAndDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CiNetworkIdentity",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CiNetworkIdentity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CiNetworkIdentity_ConfigurationItem_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkDiscoveryProfile",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Cidr = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutMs = table.Column<int>(type: "int", nullable: false),
                    MaxConcurrency = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkDiscoveryProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkLinkDetail",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelationshipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromPort = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ToPort = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MediaType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LinkMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkLinkDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkLinkDetail_CiRelationship_RelationshipId",
                        column: x => x.RelationshipId,
                        principalSchema: "cmdb",
                        principalTable: "CiRelationship",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTopologyView",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTopologyView", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NetworkDiscoveryRun",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressesScanned = table.Column<int>(type: "int", nullable: false),
                    ResponsiveHosts = table.Column<int>(type: "int", nullable: false),
                    ErrorSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkDiscoveryRun", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkDiscoveryRun_NetworkDiscoveryProfile_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "cmdb",
                        principalTable: "NetworkDiscoveryProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTopologyNodeLayout",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopologyViewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionX = table.Column<double>(type: "float", nullable: false),
                    PositionY = table.Column<double>(type: "float", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTopologyNodeLayout", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTopologyNodeLayout_ConfigurationItem_ConfigurationItemId",
                        column: x => x.ConfigurationItemId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTopologyNodeLayout_NetworkTopologyView_TopologyViewId",
                        column: x => x.TopologyViewId,
                        principalSchema: "cmdb",
                        principalTable: "NetworkTopologyView",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkDiscoveryObservation",
                schema: "cmdb",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Hostname = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Vendor = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResponseMs = table.Column<int>(type: "int", nullable: true),
                    MatchStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MatchedConfigurationItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkDiscoveryObservation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkDiscoveryObservation_ConfigurationItem_MatchedConfigurationItemId",
                        column: x => x.MatchedConfigurationItemId,
                        principalSchema: "cmdb",
                        principalTable: "ConfigurationItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NetworkDiscoveryObservation_NetworkDiscoveryRun_RunId",
                        column: x => x.RunId,
                        principalSchema: "cmdb",
                        principalTable: "NetworkDiscoveryRun",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CiNetworkIdentity_Ci_Ip",
                schema: "cmdb",
                table: "CiNetworkIdentity",
                columns: new[] { "ConfigurationItemId", "IpAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CiNetworkIdentity_ConfigurationItemId",
                schema: "cmdb",
                table: "CiNetworkIdentity",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CiNetworkIdentity_IpAddress",
                schema: "cmdb",
                table: "CiNetworkIdentity",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDiscoveryObservation_MatchedConfigurationItemId",
                schema: "cmdb",
                table: "NetworkDiscoveryObservation",
                column: "MatchedConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDiscoveryObservation_Run_Ip",
                schema: "cmdb",
                table: "NetworkDiscoveryObservation",
                columns: new[] { "RunId", "IpAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDiscoveryObservation_RunId",
                schema: "cmdb",
                table: "NetworkDiscoveryObservation",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDiscoveryProfile_Name",
                schema: "cmdb",
                table: "NetworkDiscoveryProfile",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDiscoveryRun_ProfileId",
                schema: "cmdb",
                table: "NetworkDiscoveryRun",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkDiscoveryRun_StartedAtUtc",
                schema: "cmdb",
                table: "NetworkDiscoveryRun",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkLinkDetail_RelationshipId",
                schema: "cmdb",
                table: "NetworkLinkDetail",
                column: "RelationshipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopologyNodeLayout_ConfigurationItemId",
                schema: "cmdb",
                table: "NetworkTopologyNodeLayout",
                column: "ConfigurationItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopologyNodeLayout_View_Ci",
                schema: "cmdb",
                table: "NetworkTopologyNodeLayout",
                columns: new[] { "TopologyViewId", "ConfigurationItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTopologyView_Name",
                schema: "cmdb",
                table: "NetworkTopologyView",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CiNetworkIdentity",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "NetworkDiscoveryObservation",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "NetworkLinkDetail",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "NetworkTopologyNodeLayout",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "NetworkDiscoveryRun",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "NetworkTopologyView",
                schema: "cmdb");

            migrationBuilder.DropTable(
                name: "NetworkDiscoveryProfile",
                schema: "cmdb");
        }
    }
}
