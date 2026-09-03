using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Platform.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatformFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "plt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
