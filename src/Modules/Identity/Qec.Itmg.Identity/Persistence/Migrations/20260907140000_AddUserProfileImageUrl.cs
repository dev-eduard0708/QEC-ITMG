using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileImageUrl",
                schema: "id",
                table: "User",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileImageUrl",
                schema: "id",
                table: "User");
        }
    }
}
