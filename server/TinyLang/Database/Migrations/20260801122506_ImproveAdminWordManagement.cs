using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImproveAdminWordManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "words",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_senses_PartOfSpeech_WordId",
                table: "word_senses",
                columns: new[] { "PartOfSpeech", "WordId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_word_senses_PartOfSpeech_WordId",
                table: "word_senses");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "words");
        }
    }
}
