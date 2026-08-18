using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWordStudyActivityAndCheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "word_study_activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_study_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_study_activities_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_word_study_activities_word_study_session_items_SessionItemId",
                        column: x => x.SessionItemId,
                        principalTable: "word_study_session_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_word_study_activities_word_study_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "word_study_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_word_study_activities_words_WordId",
                        column: x => x.WordId,
                        principalTable: "words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "word_study_check_ins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedInAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_study_check_ins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_word_study_check_ins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_word_study_activities_SessionId",
                table: "word_study_activities",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_word_study_activities_SessionItemId_ActivityType",
                table: "word_study_activities",
                columns: new[] { "SessionItemId", "ActivityType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_study_activities_UserId_CompletedAtUtc_WordId",
                table: "word_study_activities",
                columns: new[] { "UserId", "CompletedAtUtc", "WordId" });

            migrationBuilder.CreateIndex(
                name: "IX_word_study_activities_WordId",
                table: "word_study_activities",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_word_study_check_ins_UserId_StudyDateUtc",
                table: "word_study_check_ins",
                columns: new[] { "UserId", "StudyDateUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "word_study_activities");

            migrationBuilder.DropTable(
                name: "word_study_check_ins");
        }
    }
}
