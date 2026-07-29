using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicWordStudyModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_word_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    RememberedCount = table.Column<int>(type: "integer", nullable: false),
                    ForgottenCount = table.Column<int>(type: "integer", nullable: false),
                    LastResult = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FirstStudiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastStudiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_word_progress", x => x.Id);
                    table.CheckConstraint("CK_user_word_progress_counts", "\"ReviewCount\" >= 0 AND \"RememberedCount\" >= 0 AND \"ForgottenCount\" >= 0 AND \"ReviewCount\" = \"RememberedCount\" + \"ForgottenCount\"");
                    table.ForeignKey(
                        name: "FK_user_word_progress_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_word_progress_words_WordId",
                        column: x => x.WordId,
                        principalTable: "words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "word_study_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedCount = table.Column<int>(type: "integer", nullable: false),
                    ActualCount = table.Column<int>(type: "integer", nullable: false),
                    IncludePreviouslyStudied = table.Column<bool>(type: "boolean", nullable: false),
                    SelectionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AbandonedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_study_sessions", x => x.Id);
                    table.CheckConstraint("CK_word_study_sessions_counts", "\"RequestedCount\" BETWEEN 1 AND 100 AND \"ActualCount\" BETWEEN 1 AND \"RequestedCount\"");
                    table.ForeignKey(
                        name: "FK_word_study_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "word_study_session_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SkipReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_word_study_session_items", x => x.Id);
                    table.CheckConstraint("CK_word_study_session_items_position", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_word_study_session_items_word_study_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "word_study_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_word_study_session_items_words_WordId",
                        column: x => x.WordId,
                        principalTable: "words",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_word_progress_UserId_LastStudiedAt_WordId",
                table: "user_word_progress",
                columns: new[] { "UserId", "LastStudiedAt", "WordId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_word_progress_UserId_WordId",
                table: "user_word_progress",
                columns: new[] { "UserId", "WordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_word_progress_WordId",
                table: "user_word_progress",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_word_study_session_items_SessionId_Position",
                table: "word_study_session_items",
                columns: new[] { "SessionId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_study_session_items_SessionId_Status_Position",
                table: "word_study_session_items",
                columns: new[] { "SessionId", "Status", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_word_study_session_items_SessionId_WordId",
                table: "word_study_session_items",
                columns: new[] { "SessionId", "WordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_word_study_session_items_WordId",
                table: "word_study_session_items",
                column: "WordId");

            migrationBuilder.CreateIndex(
                name: "IX_word_study_sessions_UserId",
                table: "word_study_sessions",
                column: "UserId",
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_word_study_sessions_UserId_StartedAt_Id",
                table: "word_study_sessions",
                columns: new[] { "UserId", "StartedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_word_progress");

            migrationBuilder.DropTable(
                name: "word_study_session_items");

            migrationBuilder.DropTable(
                name: "word_study_sessions");
        }
    }
}
