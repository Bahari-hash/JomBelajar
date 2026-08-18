using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations;

/// <inheritdoc />
public partial class UpgradeWordStudyModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "word_study_session_items");
        migrationBuilder.DropTable(name: "word_study_sessions");
        migrationBuilder.DropTable(name: "user_word_progress");

        migrationBuilder.CreateSequence(name: "word_study_order_seq");

        migrationBuilder.AddColumn<long>(
            name: "StudyOrder",
            table: "words",
            type: "bigint",
            nullable: true);

        migrationBuilder.Sql(
            """
            WITH ordered_words AS (
                SELECT "Id", ROW_NUMBER() OVER (ORDER BY "CreatedAt", "Id") AS study_order
                FROM words
            )
            UPDATE words AS word
            SET "StudyOrder" = ordered_words.study_order
            FROM ordered_words
            WHERE word."Id" = ordered_words."Id";
            """);

        migrationBuilder.AlterColumn<long>(
            name: "StudyOrder",
            table: "words",
            type: "bigint",
            nullable: false,
            defaultValueSql: "nextval('word_study_order_seq')",
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);

        migrationBuilder.Sql(
            """
            SELECT setval(
                'word_study_order_seq',
                COALESCE((SELECT MAX("StudyOrder") FROM words), 0) + 1,
                false);
            """);

        migrationBuilder.AddColumn<int>(
            name: "DailyWordReviewCount",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 50);

        migrationBuilder.CreateTable(
            name: "user_word_progress",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                WordId = table.Column<Guid>(type: "uuid", nullable: false),
                FirstStudiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastStudiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewStage = table.Column<int>(type: "integer", nullable: false),
                NextReviewAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReviewCount = table.Column<int>(type: "integer", nullable: false),
                SuccessfulReviewCount = table.Column<int>(type: "integer", nullable: false),
                FailedReviewCount = table.Column<int>(type: "integer", nullable: false),
                IsReviewExcluded = table.Column<bool>(type: "boolean", nullable: false),
                ReviewExcludedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_word_progress", value => value.Id);
                table.CheckConstraint(
                    "CK_user_word_progress_review_counts",
                    "\"ReviewCount\" >= 0 AND \"SuccessfulReviewCount\" >= 0 AND \"FailedReviewCount\" >= 0 AND \"ReviewCount\" = \"SuccessfulReviewCount\" + \"FailedReviewCount\"");
                table.CheckConstraint(
                    "CK_user_word_progress_review_exclusion",
                    "(NOT \"IsReviewExcluded\" AND \"ReviewExcludedAt\" IS NULL) OR (\"IsReviewExcluded\" AND \"ReviewExcludedAt\" IS NOT NULL AND \"NextReviewAt\" IS NULL)");
                table.CheckConstraint(
                    "CK_user_word_progress_review_stage",
                    "\"ReviewStage\" BETWEEN 0 AND 5");
                table.ForeignKey(
                    name: "FK_user_word_progress_users_UserId",
                    column: value => value.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_word_progress_words_WordId",
                    column: value => value.WordId,
                    principalTable: "words",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_word_favorites",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                WordId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_word_favorites", value => value.Id);
                table.ForeignKey(
                    name: "FK_user_word_favorites_users_UserId",
                    column: value => value.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_word_favorites_words_WordId",
                    column: value => value.WordId,
                    principalTable: "words",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "word_study_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedCount = table.Column<int>(type: "integer", nullable: false),
                ActualCount = table.Column<int>(type: "integer", nullable: false),
                SessionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_word_study_sessions", value => value.Id);
                table.CheckConstraint(
                    "CK_word_study_sessions_counts",
                    "((\"SessionType\" = 'Learning' AND \"RequestedCount\" BETWEEN 1 AND 100) OR (\"SessionType\" = 'Review' AND \"RequestedCount\" BETWEEN 1 AND 200)) AND \"ActualCount\" BETWEEN 1 AND \"RequestedCount\"");
                table.ForeignKey(
                    name: "FK_word_study_sessions_users_UserId",
                    column: value => value.UserId,
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
                MemorizationQueueOrder = table.Column<long>(type: "bigint", nullable: false),
                MemorizationAttemptCount = table.Column<int>(type: "integer", nullable: false),
                HadMemorizationFailure = table.Column<bool>(type: "boolean", nullable: false),
                MemorizationPassedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SpellingQueueOrder = table.Column<long>(type: "bigint", nullable: false),
                SpellingAttemptCount = table.Column<int>(type: "integer", nullable: false),
                HadSpellingFailure = table.Column<bool>(type: "boolean", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                SkipReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_word_study_session_items", value => value.Id);
                table.CheckConstraint(
                    "CK_word_study_session_items_attempt_counts",
                    "\"MemorizationAttemptCount\" >= 0 AND \"SpellingAttemptCount\" >= 0 AND \"MemorizationQueueOrder\" >= 0 AND \"SpellingQueueOrder\" >= 0");
                table.CheckConstraint(
                    "CK_word_study_session_items_position",
                    "\"Position\" >= 0");
                table.ForeignKey(
                    name: "FK_word_study_session_items_word_study_sessions_SessionId",
                    column: value => value.SessionId,
                    principalTable: "word_study_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_word_study_session_items_words_WordId",
                    column: value => value.WordId,
                    principalTable: "words",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_words_StudyOrder",
            table: "words",
            column: "StudyOrder",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_user_word_progress_UserId_WordId",
            table: "user_word_progress",
            columns: new[] { "UserId", "WordId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_user_word_progress_UserId_NextReviewAt_WordId",
            table: "user_word_progress",
            columns: new[] { "UserId", "NextReviewAt", "WordId" });
        migrationBuilder.CreateIndex(
            name: "IX_user_word_progress_WordId",
            table: "user_word_progress",
            column: "WordId");
        migrationBuilder.CreateIndex(
            name: "IX_user_word_favorites_UserId_WordId",
            table: "user_word_favorites",
            columns: new[] { "UserId", "WordId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_user_word_favorites_WordId",
            table: "user_word_favorites",
            column: "WordId");
        migrationBuilder.CreateIndex(
            name: "IX_word_study_sessions_UserId_ActiveLearning",
            table: "word_study_sessions",
            column: "UserId",
            unique: true,
            filter: "\"Status\" = 'Active' AND \"SessionType\" = 'Learning'");
        migrationBuilder.CreateIndex(
            name: "IX_word_study_sessions_UserId_ActiveReview",
            table: "word_study_sessions",
            column: "UserId",
            unique: true,
            filter: "\"Status\" = 'Active' AND \"SessionType\" = 'Review'");
        migrationBuilder.CreateIndex(
            name: "IX_word_study_sessions_UserId_StartedAt_Id",
            table: "word_study_sessions",
            columns: new[] { "UserId", "StartedAt", "Id" });
        migrationBuilder.CreateIndex(
            name: "IX_word_study_session_items_SessionId_Position",
            table: "word_study_session_items",
            columns: new[] { "SessionId", "Position" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_word_study_session_items_SessionId_WordId",
            table: "word_study_session_items",
            columns: new[] { "SessionId", "WordId" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_word_study_items_memorization_queue",
            table: "word_study_session_items",
            columns: new[] { "SessionId", "Status", "MemorizationQueueOrder" });
        migrationBuilder.CreateIndex(
            name: "IX_word_study_items_spelling_queue",
            table: "word_study_session_items",
            columns: new[] { "SessionId", "Status", "SpellingQueueOrder" });
        migrationBuilder.CreateIndex(
            name: "IX_word_study_session_items_WordId",
            table: "word_study_session_items",
            column: "WordId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_users_daily_word_review_count",
            table: "users",
            sql: "\"DailyWordReviewCount\" BETWEEN 1 AND 200");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_word_favorites");
        migrationBuilder.DropTable(name: "word_study_session_items");
        migrationBuilder.DropTable(name: "word_study_sessions");
        migrationBuilder.DropTable(name: "user_word_progress");

        migrationBuilder.DropCheckConstraint(
            name: "CK_users_daily_word_review_count",
            table: "users");
        migrationBuilder.DropIndex(name: "IX_words_StudyOrder", table: "words");
        migrationBuilder.DropColumn(name: "StudyOrder", table: "words");
        migrationBuilder.DropColumn(name: "DailyWordReviewCount", table: "users");
        migrationBuilder.DropSequence(name: "word_study_order_seq");

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
                table.PrimaryKey("PK_user_word_progress", value => value.Id);
                table.CheckConstraint(
                    "CK_user_word_progress_counts",
                    "\"ReviewCount\" >= 0 AND \"RememberedCount\" >= 0 AND \"ForgottenCount\" >= 0 AND \"ReviewCount\" = \"RememberedCount\" + \"ForgottenCount\"");
                table.ForeignKey(
                    name: "FK_user_word_progress_users_UserId",
                    column: value => value.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_word_progress_words_WordId",
                    column: value => value.WordId,
                    principalTable: "words",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "word_study_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedCount = table.Column<int>(type: "integer", nullable: false),
                ActualCount = table.Column<int>(type: "integer", nullable: false),
                StudyDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IncludePreviouslyStudied = table.Column<bool>(type: "boolean", nullable: false),
                SelectionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                table.PrimaryKey("PK_word_study_sessions", value => value.Id);
                table.CheckConstraint(
                    "CK_word_study_sessions_counts",
                    "\"RequestedCount\" BETWEEN 1 AND 100 AND \"ActualCount\" BETWEEN 1 AND \"RequestedCount\"");
                table.ForeignKey(
                    name: "FK_word_study_sessions_users_UserId",
                    column: value => value.UserId,
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
                table.PrimaryKey("PK_word_study_session_items", value => value.Id);
                table.CheckConstraint("CK_word_study_session_items_position", "\"Position\" >= 0");
                table.ForeignKey(
                    name: "FK_word_study_session_items_word_study_sessions_SessionId",
                    column: value => value.SessionId,
                    principalTable: "word_study_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_word_study_session_items_words_WordId",
                    column: value => value.WordId,
                    principalTable: "words",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
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
            name: "IX_word_study_sessions_UserId",
            table: "word_study_sessions",
            column: "UserId",
            unique: true,
            filter: "\"Status\" = 'Active'");
        migrationBuilder.CreateIndex(
            name: "IX_word_study_sessions_UserId_StudyDateUtc",
            table: "word_study_sessions",
            columns: new[] { "UserId", "StudyDateUtc" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_word_study_sessions_UserId_StartedAt_Id",
            table: "word_study_sessions",
            columns: new[] { "UserId", "StartedAt", "Id" });
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
    }
}
