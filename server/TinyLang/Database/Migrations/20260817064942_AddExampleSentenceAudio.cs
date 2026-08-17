using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddExampleSentenceAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM word_study_session_items;
                DELETE FROM word_study_sessions;
                DELETE FROM user_word_progress;
                DELETE FROM example_sentences;
                DELETE FROM word_senses;
                DELETE FROM words;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "AudioResourceId",
                table: "example_sentences",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_example_sentences_AudioResourceId",
                table: "example_sentences",
                column: "AudioResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_example_sentences_audio_resources_AudioResourceId",
                table: "example_sentences",
                column: "AudioResourceId",
                principalTable: "audio_resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM word_study_session_items;
                DELETE FROM word_study_sessions;
                DELETE FROM user_word_progress;
                DELETE FROM example_sentences;
                DELETE FROM word_senses;
                DELETE FROM words;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_example_sentences_audio_resources_AudioResourceId",
                table: "example_sentences");

            migrationBuilder.DropIndex(
                name: "IX_example_sentences_AudioResourceId",
                table: "example_sentences");

            migrationBuilder.DropColumn(
                name: "AudioResourceId",
                table: "example_sentences");
        }
    }
}
