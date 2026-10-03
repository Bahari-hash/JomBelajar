using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFsrsWordScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MemorizationAvailableAt",
                table: "word_study_session_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DesiredRetention",
                table: "users",
                type: "double precision",
                nullable: false,
                defaultValue: 0.90000000000000002);

            migrationBuilder.AddColumn<double>(
                name: "Difficulty",
                table: "user_word_progress",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FsrsState",
                table: "user_word_progress",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ImportedFromLegacy",
                table: "user_word_progress",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRatedAt",
                table: "user_word_progress",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LearningStep",
                table: "user_word_progress",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchedulerVersion",
                table: "user_word_progress",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Stability",
                table: "user_word_progress",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "word_review_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DesiredRetention = table.Column<double>(type: "double precision", nullable: false),
                    SchedulerVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImportedFromLegacy = table.Column<bool>(type: "boolean", nullable: false),
                    BeforeStateJson = table.Column<string>(type: "text", nullable: false),
                    AfterStateJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_review_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_review_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_word_review_logs_word_study_session_items_SessionItemId",
                        column: x => x.SessionItemId,
                        principalTable: "word_study_session_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_review_logs_SessionItemId_SubmissionStamp",
                table: "word_review_logs",
                columns: new[] { "SessionItemId", "SubmissionStamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_review_logs_UserId_WordId_RatedAt",
                table: "word_review_logs",
                columns: new[] { "UserId", "WordId", "RatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "word_review_logs");

            migrationBuilder.DropColumn(
                name: "MemorizationAvailableAt",
                table: "word_study_session_items");

            migrationBuilder.DropColumn(
                name: "DesiredRetention",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "user_word_progress");

            migrationBuilder.DropColumn(
                name: "FsrsState",
                table: "user_word_progress");

            migrationBuilder.DropColumn(
                name: "ImportedFromLegacy",
                table: "user_word_progress");

            migrationBuilder.DropColumn(
                name: "LastRatedAt",
                table: "user_word_progress");

            migrationBuilder.DropColumn(
                name: "LearningStep",
                table: "user_word_progress");

            migrationBuilder.DropColumn(
                name: "SchedulerVersion",
                table: "user_word_progress");

            migrationBuilder.DropColumn(
                name: "Stability",
                table: "user_word_progress");
        }
    }
}
