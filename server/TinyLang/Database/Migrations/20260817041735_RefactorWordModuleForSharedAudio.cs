using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class RefactorWordModuleForSharedAudio : Migration
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
                DELETE FROM word_pronunciations;
                DELETE FROM words;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_user_word_progress_words_WordId",
                table: "user_word_progress");

            migrationBuilder.DropForeignKey(
                name: "FK_word_study_session_items_words_WordId",
                table: "word_study_session_items");

            migrationBuilder.DropForeignKey(
                name: "FK_words_users_CreatedById",
                table: "words");

            migrationBuilder.DropForeignKey(
                name: "FK_words_users_LastEditorId",
                table: "words");

            migrationBuilder.DropTable(
                name: "word_pronunciations");

            migrationBuilder.DropIndex(
                name: "IX_words_CreatedById",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_LastEditorId",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_Status_PublishedAt_Id",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_Status_UpdatedAt_Id",
                table: "words");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "words");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "words");

            migrationBuilder.DropColumn(
                name: "LastEditorId",
                table: "words");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "words");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "words");

            migrationBuilder.AddColumn<Guid>(
                name: "AudioResourceId",
                table: "words",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_words_AudioResourceId",
                table: "words",
                column: "AudioResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_words_UpdatedAt_Id",
                table: "words",
                columns: new[] { "UpdatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_user_word_progress_words_WordId",
                table: "user_word_progress",
                column: "WordId",
                principalTable: "words",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_word_study_session_items_words_WordId",
                table: "word_study_session_items",
                column: "WordId",
                principalTable: "words",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_words_audio_resources_AudioResourceId",
                table: "words",
                column: "AudioResourceId",
                principalTable: "audio_resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_word_progress_words_WordId",
                table: "user_word_progress");

            migrationBuilder.DropForeignKey(
                name: "FK_word_study_session_items_words_WordId",
                table: "word_study_session_items");

            migrationBuilder.DropForeignKey(
                name: "FK_words_audio_resources_AudioResourceId",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_AudioResourceId",
                table: "words");

            migrationBuilder.DropIndex(
                name: "IX_words_UpdatedAt_Id",
                table: "words");

            migrationBuilder.DropColumn(
                name: "AudioResourceId",
                table: "words");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "words",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "words",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditorId",
                table: "words",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "words",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "words",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "word_pronunciations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccentTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ipa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_pronunciations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_pronunciations_words_WordId",
                        column: x => x.WordId,
                        principalTable: "words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_words_CreatedById",
                table: "words",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_words_LastEditorId",
                table: "words",
                column: "LastEditorId");

            migrationBuilder.CreateIndex(
                name: "IX_words_Status_PublishedAt_Id",
                table: "words",
                columns: new[] { "Status", "PublishedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_words_Status_UpdatedAt_Id",
                table: "words",
                columns: new[] { "Status", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_WordId",
                table: "word_pronunciations",
                column: "WordId",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_WordId_SortOrder",
                table: "word_pronunciations",
                columns: new[] { "WordId", "SortOrder" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_user_word_progress_words_WordId",
                table: "user_word_progress",
                column: "WordId",
                principalTable: "words",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_word_study_session_items_words_WordId",
                table: "word_study_session_items",
                column: "WordId",
                principalTable: "words",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_words_users_CreatedById",
                table: "words",
                column: "CreatedById",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_words_users_LastEditorId",
                table: "words",
                column: "LastEditorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
