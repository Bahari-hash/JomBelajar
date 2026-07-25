using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeMediaUploadModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "media_resources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "StagingObjectName",
                table: "media_resources",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UploadExpiresAt",
                table: "media_resources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "multipart_upload_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderUploadId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PartSize = table.Column<long>(type: "bigint", nullable: false),
                    PartCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StagingCleanupRequired = table.Column<bool>(type: "boolean", nullable: false),
                    LastFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_multipart_upload_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_multipart_upload_sessions_media_resources_MediaResourceId",
                        column: x => x.MediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_multipart_upload_sessions_users_UploaderId",
                        column: x => x.UploaderId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_media_resources_StagingObjectName",
                table: "media_resources",
                column: "StagingObjectName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_resources_Status_UploadExpiresAt_Id",
                table: "media_resources",
                columns: new[] { "Status", "UploadExpiresAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_multipart_upload_sessions_MediaResourceId",
                table: "multipart_upload_sessions",
                column: "MediaResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_multipart_upload_sessions_Status_NextAttemptAt_LeaseExpires~",
                table: "multipart_upload_sessions",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_multipart_upload_sessions_UploaderId_Status",
                table: "multipart_upload_sessions",
                columns: new[] { "UploaderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "multipart_upload_sessions");

            migrationBuilder.DropIndex(
                name: "IX_media_resources_StagingObjectName",
                table: "media_resources");

            migrationBuilder.DropIndex(
                name: "IX_media_resources_Status_UploadExpiresAt_Id",
                table: "media_resources");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "media_resources");

            migrationBuilder.DropColumn(
                name: "StagingObjectName",
                table: "media_resources");

            migrationBuilder.DropColumn(
                name: "UploadExpiresAt",
                table: "media_resources");
        }
    }
}
