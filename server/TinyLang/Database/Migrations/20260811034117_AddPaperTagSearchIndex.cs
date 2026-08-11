using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperTagSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_papers_Tags",
                table: "papers",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_papers_Tags",
                table: "papers");
        }
    }
}
