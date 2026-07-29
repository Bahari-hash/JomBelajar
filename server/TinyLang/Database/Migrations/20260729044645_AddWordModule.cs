using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWordModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "words",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Headword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedHeadword = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_words", x => x.Id);
                    table.ForeignKey(
                        name: "FK_words_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_words_users_LastEditorId",
                        column: x => x.LastEditorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "word_pronunciations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioClipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccentTag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ipa = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_pronunciations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_pronunciations_audio_clips_AudioClipId",
                        column: x => x.AudioClipId,
                        principalTable: "audio_clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_word_pronunciations_words_WordId",
                        column: x => x.WordId,
                        principalTable: "words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "word_senses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartOfSpeech = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Definition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DefinitionLanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    UsageNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_senses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_senses_words_WordId",
                        column: x => x.WordId,
                        principalTable: "words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "example_sentences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WordSenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sentence = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Translation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TranslationLanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    AudioClipId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_example_sentences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_example_sentences_audio_clips_AudioClipId",
                        column: x => x.AudioClipId,
                        principalTable: "audio_clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_example_sentences_word_senses_WordSenseId",
                        column: x => x.WordSenseId,
                        principalTable: "word_senses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_example_sentences_AudioClipId",
                table: "example_sentences",
                column: "AudioClipId");

            migrationBuilder.CreateIndex(
                name: "IX_example_sentences_WordSenseId_SortOrder",
                table: "example_sentences",
                columns: new[] { "WordSenseId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_AudioClipId",
                table: "word_pronunciations",
                column: "AudioClipId");

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_WordId",
                table: "word_pronunciations",
                column: "WordId",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_WordId_AudioClipId",
                table: "word_pronunciations",
                columns: new[] { "WordId", "AudioClipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_WordId_SortOrder",
                table: "word_pronunciations",
                columns: new[] { "WordId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_senses_WordId_SortOrder",
                table: "word_senses",
                columns: new[] { "WordId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_words_CreatedById",
                table: "words",
                column: "CreatedById");

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
                name: "IX_words_LastEditorId",
                table: "words",
                column: "LastEditorId");

            migrationBuilder.CreateIndex(
                name: "IX_words_Status_LanguageTag_PublishedAt_Id",
                table: "words",
                columns: new[] { "Status", "LanguageTag", "PublishedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "example_sentences");

            migrationBuilder.DropTable(
                name: "word_pronunciations");

            migrationBuilder.DropTable(
                name: "word_senses");

            migrationBuilder.DropTable(
                name: "words");
        }
    }
}
