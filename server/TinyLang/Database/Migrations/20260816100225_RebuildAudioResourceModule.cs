using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class RebuildAudioResourceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_example_sentences_audio_clips_AudioClipId",
                table: "example_sentences");

            migrationBuilder.DropForeignKey(
                name: "FK_word_pronunciations_audio_clips_AudioClipId",
                table: "word_pronunciations");

            migrationBuilder.DropTable(
                name: "audio_processing_jobs");

            migrationBuilder.DropTable(
                name: "audio_clips");

            migrationBuilder.DropIndex(
                name: "IX_word_pronunciations_AudioClipId",
                table: "word_pronunciations");

            migrationBuilder.DropIndex(
                name: "IX_word_pronunciations_WordId_AudioClipId",
                table: "word_pronunciations");

            migrationBuilder.DropIndex(
                name: "IX_example_sentences_AudioClipId",
                table: "example_sentences");

            migrationBuilder.DropColumn(
                name: "AudioClipId",
                table: "word_pronunciations");

            migrationBuilder.DropColumn(
                name: "AudioClipId",
                table: "example_sentences");

            migrationBuilder.CreateTable(
                name: "audio_resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SourceMediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    SampleRate = table.Column<int>(type: "integer", nullable: true),
                    Channels = table.Column<int>(type: "integer", nullable: true),
                    ContainerFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceCodec = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentOutputVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_resources_media_resources_SourceMediaResourceId",
                        column: x => x.SourceMediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audio_resources_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audio_resources_users_LastEditorId",
                        column: x => x.LastEditorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audio_processing_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_processing_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_processing_jobs_audio_resources_AudioResourceId",
                        column: x => x.AudioResourceId,
                        principalTable: "audio_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audio_resources_CreatedById",
                table: "audio_resources",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_audio_resources_LastEditorId",
                table: "audio_resources",
                column: "LastEditorId");

            migrationBuilder.CreateIndex(
                name: "IX_audio_resources_NormalizedName",
                table: "audio_resources",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_resources_SourceMediaResourceId",
                table: "audio_resources",
                column: "SourceMediaResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_resources_Status_UpdatedAt_Id",
                table: "audio_resources",
                columns: new[] { "Status", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_audio_processing_jobs_AudioResourceId",
                table: "audio_processing_jobs",
                column: "AudioResourceId",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_audio_processing_jobs_AudioResourceId_OutputVersion",
                table: "audio_processing_jobs",
                columns: new[] { "AudioResourceId", "OutputVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_L~",
                table: "audio_processing_jobs",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "LastDispatchedAt", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_processing_jobs");

            migrationBuilder.DropTable(
                name: "audio_resources");

            migrationBuilder.Sql("DELETE FROM word_pronunciations;");

            migrationBuilder.AddColumn<Guid>(
                name: "AudioClipId",
                table: "word_pronunciations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "AudioClipId",
                table: "example_sentences",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audio_clips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channels = table.Column<int>(type: "integer", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentOutputVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OutputObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublicationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SampleRate = table.Column<int>(type: "integer", nullable: true),
                    SourceCodec = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_clips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_clips_media_resources_SourceMediaResourceId",
                        column: x => x.SourceMediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audio_clips_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audio_clips_users_LastEditorId",
                        column: x => x.LastEditorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audio_processing_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AudioClipId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audio_processing_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audio_processing_jobs_audio_clips_AudioClipId",
                        column: x => x.AudioClipId,
                        principalTable: "audio_clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_AudioClipId",
                table: "word_pronunciations",
                column: "AudioClipId");

            migrationBuilder.CreateIndex(
                name: "IX_word_pronunciations_WordId_AudioClipId",
                table: "word_pronunciations",
                columns: new[] { "WordId", "AudioClipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_example_sentences_AudioClipId",
                table: "example_sentences",
                column: "AudioClipId");

            migrationBuilder.CreateIndex(
                name: "IX_audio_clips_CreatedById_ProcessingStatus_PublicationStatus_~",
                table: "audio_clips",
                columns: new[] { "CreatedById", "ProcessingStatus", "PublicationStatus", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_audio_clips_LastEditorId",
                table: "audio_clips",
                column: "LastEditorId");

            migrationBuilder.CreateIndex(
                name: "IX_audio_clips_ProcessingStatus_PublicationStatus_Id",
                table: "audio_clips",
                columns: new[] { "ProcessingStatus", "PublicationStatus", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_audio_clips_SourceMediaResourceId",
                table: "audio_clips",
                column: "SourceMediaResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_processing_jobs_AudioClipId",
                table: "audio_processing_jobs",
                column: "AudioClipId",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_audio_processing_jobs_AudioClipId_OutputVersion",
                table: "audio_processing_jobs",
                columns: new[] { "AudioClipId", "OutputVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audio_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_L~",
                table: "audio_processing_jobs",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "LastDispatchedAt", "CreatedAt", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_example_sentences_audio_clips_AudioClipId",
                table: "example_sentences",
                column: "AudioClipId",
                principalTable: "audio_clips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_word_pronunciations_audio_clips_AudioClipId",
                table: "word_pronunciations",
                column: "AudioClipId",
                principalTable: "audio_clips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
