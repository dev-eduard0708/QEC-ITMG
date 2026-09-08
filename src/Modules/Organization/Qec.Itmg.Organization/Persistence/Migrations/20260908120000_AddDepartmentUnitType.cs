using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Organization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentUnitType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitType",
                schema: "org",
                table: "Department",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Department");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitType",
                schema: "org",
                table: "Department");
        }
    }
}
