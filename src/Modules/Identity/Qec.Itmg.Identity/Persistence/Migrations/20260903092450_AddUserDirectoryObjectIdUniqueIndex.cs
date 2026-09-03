using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qec.Itmg.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDirectoryObjectIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_User_DirectoryObjectId",
                schema: "id",
                table: "User",
                column: "DirectoryObjectId",
                unique: true,
                filter: "[DirectoryObjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_DirectoryObjectId",
                schema: "id",
                table: "User");
        }
    }
}
