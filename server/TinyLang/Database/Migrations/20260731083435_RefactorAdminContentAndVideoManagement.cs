using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAdminContentAndVideoManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "users"
                SET "Role" = 'User',
                    "TokenVersion" = "TokenVersion" + 1
                WHERE "Role" = 'Editor';
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "video_subtitles"
                WHERE "MediaResourceId" IN (
                    SELECT "Id"
                    FROM "media_resources"
                    WHERE "Module" = 'VideoSubtitle'
                );

                DELETE FROM "multipart_upload_sessions"
                WHERE "MediaResourceId" IN (
                    SELECT "Id"
                    FROM "media_resources"
                    WHERE "Module" = 'VideoSubtitle'
                );

                DELETE FROM "media_resources"
                WHERE "Module" = 'VideoSubtitle';
                """);

            migrationBuilder.DropTable(
                name: "video_subtitles");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "videos",
                newName: "CreatedById");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "audio_clips",
                newName: "CreatedById");

            migrationBuilder.RenameIndex(
                name: "IX_videos_OwnerId_ProcessingStatus_PublicationStatus_UpdatedAt~",
                table: "videos",
                newName: "IX_videos_CreatedById_ProcessingStatus_PublicationStatus_Updat~");

            migrationBuilder.RenameIndex(
                name: "IX_audio_clips_OwnerId_ProcessingStatus_PublicationStatus_Upda~",
                table: "audio_clips",
                newName: "IX_audio_clips_CreatedById_ProcessingStatus_PublicationStatus_~");

            migrationBuilder.Sql(
                """
                ALTER TABLE "videos"
                RENAME CONSTRAINT "FK_videos_users_OwnerId"
                TO "FK_videos_users_CreatedById";

                ALTER TABLE "audio_clips"
                RENAME CONSTRAINT "FK_audio_clips_users_OwnerId"
                TO "FK_audio_clips_users_CreatedById";
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditorId",
                table: "videos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditorId",
                table: "audio_clips",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "videos"
                SET "LastEditorId" = "CreatedById";

                UPDATE "audio_clips"
                SET "LastEditorId" = "CreatedById";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "LastEditorId",
                table: "videos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "LastEditorId",
                table: "audio_clips",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_video_category_assignments_video_categories_VideoCategoryId",
                table: "video_category_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_videos_LastEditorId",
                table: "videos",
                column: "LastEditorId");

            migrationBuilder.CreateIndex(
                name: "IX_videos_PublicationStatus_UpdatedAt_Id",
                table: "videos",
                columns: new[] { "PublicationStatus", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_audio_clips_LastEditorId",
                table: "audio_clips",
                column: "LastEditorId");

            migrationBuilder.AddForeignKey(
                name: "FK_audio_clips_users_LastEditorId",
                table: "audio_clips",
                column: "LastEditorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_video_category_assignments_video_categories",
                table: "video_category_assignments",
                column: "VideoCategoryId",
                principalTable: "video_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_videos_users_LastEditorId",
                table: "videos",
                column: "LastEditorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audio_clips_users_LastEditorId",
                table: "audio_clips");

            migrationBuilder.DropForeignKey(
                name: "FK_video_category_assignments_video_categories",
                table: "video_category_assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_videos_users_LastEditorId",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_LastEditorId",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_videos_PublicationStatus_UpdatedAt_Id",
                table: "videos");

            migrationBuilder.DropIndex(
                name: "IX_audio_clips_LastEditorId",
                table: "audio_clips");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "LastEditorId",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "LastEditorId",
                table: "audio_clips");

            migrationBuilder.Sql(
                """
                ALTER TABLE "videos"
                RENAME CONSTRAINT "FK_videos_users_CreatedById"
                TO "FK_videos_users_OwnerId";

                ALTER TABLE "audio_clips"
                RENAME CONSTRAINT "FK_audio_clips_users_CreatedById"
                TO "FK_audio_clips_users_OwnerId";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_videos_CreatedById_ProcessingStatus_PublicationStatus_Updat~",
                table: "videos",
                newName: "IX_videos_OwnerId_ProcessingStatus_PublicationStatus_UpdatedAt~");

            migrationBuilder.RenameIndex(
                name: "IX_audio_clips_CreatedById_ProcessingStatus_PublicationStatus_~",
                table: "audio_clips",
                newName: "IX_audio_clips_OwnerId_ProcessingStatus_PublicationStatus_Upda~");

            migrationBuilder.RenameColumn(
                name: "CreatedById",
                table: "videos",
                newName: "OwnerId");

            migrationBuilder.RenameColumn(
                name: "CreatedById",
                table: "audio_clips",
                newName: "OwnerId");

            migrationBuilder.CreateTable(
                name: "video_subtitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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

            migrationBuilder.AddForeignKey(
                name: "FK_video_category_assignments_video_categories_VideoCategoryId",
                table: "video_category_assignments",
                column: "VideoCategoryId",
                principalTable: "video_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
