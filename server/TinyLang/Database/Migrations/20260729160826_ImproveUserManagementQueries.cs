using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImproveUserManagementQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_IsDeleted_CreatedAt_Id",
                table: "users",
                columns: new[] { "IsDeleted", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_users_IsDeleted_IsBanned_CreatedAt_Id",
                table: "users",
                columns: new[] { "IsDeleted", "IsBanned", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_users_IsDeleted_Role_CreatedAt_Id",
                table: "users",
                columns: new[] { "IsDeleted", "Role", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_IsDeleted_CreatedAt_Id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_IsDeleted_IsBanned_CreatedAt_Id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_IsDeleted_Role_CreatedAt_Id",
                table: "users");
        }
    }
}
