using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class ImplementVideoModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OriginalLanguage = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    DisplayWidth = table.Column<int>(type: "integer", nullable: true),
                    DisplayHeight = table.Column<int>(type: "integer", nullable: true),
                    ContainerFormat = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VideoCodec = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AudioCodec = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProcessingStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublicationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentOutputVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    MasterPlaylistObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PosterObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_videos_media_resources_SourceMediaResourceId",
                        column: x => x.SourceMediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_videos_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_video_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionSeconds = table.Column<double>(type: "double precision", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastPlayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_video_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_video_progress_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_video_progress_videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_processing_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_video_processing_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_video_processing_jobs_videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_renditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetHeight = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    VideoBitrateKbps = table.Column<int>(type: "integer", nullable: false),
                    AudioBitrateKbps = table.Column<int>(type: "integer", nullable: false),
                    PlaylistObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Codecs = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_renditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_video_renditions_videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_subtitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_subtitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_video_subtitles_media_resources_MediaResourceId",
                        column: x => x.MediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_video_subtitles_videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_video_progress_UserId_LastPlayedAt",
                table: "user_video_progress",
                columns: new[] { "UserId", "LastPlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_video_progress_UserId_VideoId",
                table: "user_video_progress",
                columns: new[] { "UserId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_video_progress_VideoId",
                table: "user_video_progress",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_C~",
                table: "video_processing_jobs",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_VideoId",
                table: "video_processing_jobs",
                column: "VideoId",
                unique: true,
                filter: "\"Status\" IN ('Queued', 'Processing')");

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_VideoId_OutputVersion",
                table: "video_processing_jobs",
                columns: new[] { "VideoId", "OutputVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_renditions_VideoId_OutputVersion_TargetHeight",
                table: "video_renditions",
                columns: new[] { "VideoId", "OutputVersion", "TargetHeight" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_subtitles_MediaResourceId",
                table: "video_subtitles",
                column: "MediaResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_subtitles_VideoId",
                table: "video_subtitles",
                column: "VideoId",
                unique: true,
                filter: "\"IsDefault\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_video_subtitles_VideoId_LanguageTag",
                table: "video_subtitles",
                columns: new[] { "VideoId", "LanguageTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_videos_OwnerId_ProcessingStatus_PublicationStatus_UpdatedAt~",
                table: "videos",
                columns: new[] { "OwnerId", "ProcessingStatus", "PublicationStatus", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_videos_ProcessingStatus_PublicationStatus_PublishedAt_Id",
                table: "videos",
                columns: new[] { "ProcessingStatus", "PublicationStatus", "PublishedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_videos_SourceMediaResourceId",
                table: "videos",
                column: "SourceMediaResourceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_video_progress");

            migrationBuilder.DropTable(
                name: "video_processing_jobs");

            migrationBuilder.DropTable(
                name: "video_renditions");

            migrationBuilder.DropTable(
                name: "video_subtitles");

            migrationBuilder.DropTable(
                name: "videos");
        }
    }
}
