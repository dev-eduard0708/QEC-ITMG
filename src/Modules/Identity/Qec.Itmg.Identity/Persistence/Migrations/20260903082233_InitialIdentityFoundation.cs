using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
