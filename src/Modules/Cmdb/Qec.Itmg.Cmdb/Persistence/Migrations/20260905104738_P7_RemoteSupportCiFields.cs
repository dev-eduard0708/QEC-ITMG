using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Cmdb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P7_RemoteSupportCiFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RemoteEngineNodeId",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemoteEngineProvider",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UnattendedRemotePermitted",
                schema: "cmdb",
                table: "ConfigurationItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationItem_RemoteEngineNodeId",
                schema: "cmdb",
                table: "ConfigurationItem",
                column: "RemoteEngineNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConfigurationItem_RemoteEngineNodeId",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "RemoteEngineNodeId",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "RemoteEngineProvider",
                schema: "cmdb",
                table: "ConfigurationItem");

            migrationBuilder.DropColumn(
                name: "UnattendedRemotePermitted",
                schema: "cmdb",
                table: "ConfigurationItem");
        }
    }
}
