using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audio_clips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    SampleRate = table.Column<int>(type: "integer", nullable: true),
                    Channels = table.Column<int>(type: "integer", nullable: true),
                    ContainerFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceCodec = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublicationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentOutputVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    OutputObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                        name: "FK_audio_clips_users_OwnerId",
                        column: x => x.OwnerId,
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
                name: "IX_audio_clips_OwnerId_ProcessingStatus_PublicationStatus_Upda~",
                table: "audio_clips",
                columns: new[] { "OwnerId", "ProcessingStatus", "PublicationStatus", "UpdatedAt", "Id" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audio_processing_jobs");

            migrationBuilder.DropTable(
                name: "audio_clips");
        }
    }
}
