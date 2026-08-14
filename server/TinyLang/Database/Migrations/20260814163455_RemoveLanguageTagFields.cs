using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLanguageTagFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_words_LanguageTag_NormalizedHeadword",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_LanguageTag_Status_UpdatedAt_Id",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_Status_LanguageTag_PublishedAt_Id",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_papers_Status_LanguageTag_PublishedAt_Id",
                table: "papers");

            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "words");

            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "word_study_sessions");

            migrationBuilder.DropColumn(
                name: "DefinitionLanguageTag",
                table: "word_senses");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "papers");

            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "example_sentences");

            migrationBuilder.DropColumn(
                name: "TranslationLanguageTag",
                table: "example_sentences");

            migrationBuilder.DropColumn(
                name: "LanguageTag",
                table: "audio_clips");

            migrationBuilder.CreateIndex(
                name: "IX_words_NormalizedHeadword",
                table: "words",
                column: "NormalizedHeadword",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_words_Status_PublishedAt_Id",
                table: "words",
                columns: new[] { "Status", "PublishedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_words_Status_UpdatedAt_Id",
                table: "words",
                columns: new[] { "Status", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_papers_Status_PublishedAt_Id",
                table: "papers",
                columns: new[] { "Status", "PublishedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_words_NormalizedHeadword",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_Status_PublishedAt_Id",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_Status_UpdatedAt_Id",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_papers_Status_PublishedAt_Id",
                table: "papers");

            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "words",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "word_study_sessions",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefinitionLanguageTag",
                table: "word_senses",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "videos",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "papers",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "example_sentences",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TranslationLanguageTag",
                table: "example_sentences",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LanguageTag",
                table: "audio_clips",
                type: "character varying(35)",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_words_LanguageTag_NormalizedHeadword",
                table: "words",
                columns: new[] { "LanguageTag", "NormalizedHeadword" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_words_LanguageTag_Status_UpdatedAt_Id",
                table: "words",
                columns: new[] { "LanguageTag", "Status", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_words_Status_LanguageTag_PublishedAt_Id",
                table: "words",
                columns: new[] { "Status", "LanguageTag", "PublishedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_papers_Status_LanguageTag_PublishedAt_Id",
                table: "papers",
                columns: new[] { "Status", "LanguageTag", "PublishedAt", "Id" });
        }
    }
}
