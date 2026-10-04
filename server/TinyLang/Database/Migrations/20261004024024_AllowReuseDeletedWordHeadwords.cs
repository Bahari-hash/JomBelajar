using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AllowReuseDeletedWordHeadwords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_words_NormalizedHeadword",
                table: "words");

            migrationBuilder.CreateIndex(
                name: "IX_words_NormalizedHeadword",
                table: "words",
                column: "NormalizedHeadword",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_words_NormalizedHeadword",
                table: "words");

            migrationBuilder.CreateIndex(
                name: "IX_words_NormalizedHeadword",
                table: "words",
                column: "NormalizedHeadword",
                unique: true);
        }
    }
}
