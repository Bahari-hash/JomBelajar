using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateVideoProcessingWithRabbitMq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_video_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_C~",
                table: "video_processing_jobs");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDispatchedAt",
                table: "video_processing_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_L~",
                table: "video_processing_jobs",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "LastDispatchedAt", "CreatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_video_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_L~",
                table: "video_processing_jobs");

            migrationBuilder.DropColumn(
                name: "LastDispatchedAt",
                table: "video_processing_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_video_processing_jobs_Status_NextAttemptAt_LeaseExpiresAt_C~",
                table: "video_processing_jobs",
                columns: new[] { "Status", "NextAttemptAt", "LeaseExpiresAt", "CreatedAt", "Id" });
        }
    }
}
