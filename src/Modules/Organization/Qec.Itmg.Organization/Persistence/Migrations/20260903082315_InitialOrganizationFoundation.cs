using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Organization.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrganizationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "org");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
