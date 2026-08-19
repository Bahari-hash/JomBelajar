using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations;

/// <summary>
/// Destructively rebuilds the online quiz schema. Existing quiz rows are intentionally discarded.
/// </summary>
public partial class RebuildOnlineQuizSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS
                "paper_attempt_answers",
                "paper_category_assignments",
                "paper_wrong_questions",
                "paper_dictation_blanks",
                "fill_blank_accepted_answers",
                "paper_question_options",
                "paper_attempts",
                "paper_questions",
                "papers",
                "paper_categories"
            CASCADE;

            CREATE TABLE "papers" (
                "Id" uuid NOT NULL,
                "Title" character varying(200) NOT NULL,
                "Description" character varying(2000),
                "Instructions" character varying(5000),
                "Status" character varying(20) NOT NULL,
                "PassingScorePercentage" integer NOT NULL DEFAULT 60,
                "PassingScore" integer NOT NULL,
                "TotalScore" integer NOT NULL,
                "CreatedById" uuid NOT NULL,
                "LastEditorId" uuid NOT NULL,
                "PublishedAt" timestamp with time zone,
                "ArchivedAt" timestamp with time zone,
                "ConcurrencyStamp" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_papers" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_papers_scores" CHECK ("TotalScore" BETWEEN 0 AND 20000 AND "PassingScorePercentage" BETWEEN 1 AND 100 AND "PassingScore" BETWEEN 0 AND "TotalScore"),
                CONSTRAINT "FK_papers_users_CreatedById" FOREIGN KEY ("CreatedById") REFERENCES "users" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_papers_users_LastEditorId" FOREIGN KEY ("LastEditorId") REFERENCES "users" ("Id") ON DELETE RESTRICT
            );

            CREATE TABLE "paper_categories" (
                "Id" uuid NOT NULL,
                "Name" character varying(100) NOT NULL,
                "Slug" character varying(120) NOT NULL,
                "Description" character varying(500),
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_paper_categories" PRIMARY KEY ("Id")
            );

            CREATE TABLE "paper_questions" (
                "Id" uuid NOT NULL,
                "PaperId" uuid NOT NULL,
                "Type" character varying(20) NOT NULL,
                "Prompt" character varying(5000) NOT NULL,
                "Explanation" character varying(5000),
                "Points" integer NOT NULL,
                "SortOrder" integer NOT NULL,
                "CorrectBoolean" boolean,
                "FillBlankCaseSensitive" boolean NOT NULL,
                "AudioResourceId" uuid,
                CONSTRAINT "PK_paper_questions" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_paper_questions_points" CHECK ("Points" BETWEEN 1 AND 100),
                CONSTRAINT "CK_paper_questions_sort_order" CHECK ("SortOrder" BETWEEN 0 AND 10000),
                CONSTRAINT "CK_paper_questions_type_fields" CHECK (("Type" = 'TrueFalse' OR "CorrectBoolean" IS NULL) AND ("Type" = 'FillBlank' OR "FillBlankCaseSensitive" = FALSE) AND ("Type" = 'Dictation' OR "AudioResourceId" IS NULL)),
                CONSTRAINT "FK_paper_questions_papers_PaperId" FOREIGN KEY ("PaperId") REFERENCES "papers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_paper_questions_audio_resources_AudioResourceId" FOREIGN KEY ("AudioResourceId") REFERENCES "audio_resources" ("Id") ON DELETE RESTRICT
            );

            CREATE TABLE "fill_blank_accepted_answers" (
                "Id" uuid NOT NULL,
                "QuestionId" uuid NOT NULL,
                "Text" character varying(1000) NOT NULL,
                "NormalizedText" character varying(1000) NOT NULL,
                "SortOrder" integer NOT NULL,
                CONSTRAINT "PK_fill_blank_accepted_answers" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_fill_blank_accepted_answers_sort_order" CHECK ("SortOrder" BETWEEN 0 AND 10000),
                CONSTRAINT "FK_fill_blank_accepted_answers_paper_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "paper_questions" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "paper_question_options" (
                "Id" uuid NOT NULL,
                "QuestionId" uuid NOT NULL,
                "Text" character varying(2000) NOT NULL,
                "IsCorrect" boolean NOT NULL,
                "SortOrder" integer NOT NULL,
                CONSTRAINT "PK_paper_question_options" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_paper_question_options_sort_order" CHECK ("SortOrder" BETWEEN 0 AND 10000),
                CONSTRAINT "FK_paper_question_options_paper_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "paper_questions" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "paper_dictation_blanks" (
                "Id" uuid NOT NULL,
                "QuestionId" uuid NOT NULL,
                "Answer" character varying(1000) NOT NULL,
                "NormalizedAnswer" character varying(1000) NOT NULL,
                "SortOrder" integer NOT NULL,
                CONSTRAINT "PK_paper_dictation_blanks" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_paper_dictation_blanks_sort_order" CHECK ("SortOrder" BETWEEN 0 AND 10000),
                CONSTRAINT "FK_paper_dictation_blanks_paper_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "paper_questions" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "paper_attempts" (
                "Id" uuid NOT NULL,
                "PaperId" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "AttemptNumber" integer NOT NULL,
                "Status" character varying(20) NOT NULL,
                "PaperTotalScore" integer NOT NULL,
                "PaperPassingScore" integer NOT NULL,
                "Score" integer,
                "IsPassed" boolean,
                "StartedAt" timestamp with time zone NOT NULL,
                "SubmittedAt" timestamp with time zone,
                "ConcurrencyStamp" uuid NOT NULL,
                CONSTRAINT "PK_paper_attempts" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_paper_attempts_number" CHECK ("AttemptNumber" >= 1),
                CONSTRAINT "CK_paper_attempts_snapshot_scores" CHECK ("PaperTotalScore" BETWEEN 0 AND 20000 AND "PaperPassingScore" BETWEEN 0 AND "PaperTotalScore"),
                CONSTRAINT "CK_paper_attempts_submission_state" CHECK (("Status" = 'InProgress' AND "Score" IS NULL AND "IsPassed" IS NULL AND "SubmittedAt" IS NULL) OR ("Status" = 'Submitted' AND "Score" IS NOT NULL AND "Score" BETWEEN 0 AND "PaperTotalScore" AND "IsPassed" IS NOT NULL AND "SubmittedAt" IS NOT NULL)),
                CONSTRAINT "FK_paper_attempts_papers_PaperId" FOREIGN KEY ("PaperId") REFERENCES "papers" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_paper_attempts_users_UserId" FOREIGN KEY ("UserId") REFERENCES "users" ("Id") ON DELETE CASCADE
            );

            CREATE TABLE "paper_attempt_answers" (
                "Id" uuid NOT NULL,
                "AttemptId" uuid NOT NULL,
                "QuestionId" uuid NOT NULL,
                "SelectedOptionId" uuid,
                "BooleanAnswer" boolean,
                "TextAnswer" character varying(1000),
                "NormalizedTextAnswer" character varying(1000),
                "TextAnswers" text[],
                "IsAnswered" boolean NOT NULL,
                "IsCorrect" boolean,
                "AwardedPoints" integer,
                "SavedAt" timestamp with time zone,
                "ConcurrencyStamp" uuid NOT NULL,
                CONSTRAINT "PK_paper_attempt_answers" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_paper_attempt_answers_awarded_points" CHECK (("IsCorrect" IS NULL AND "AwardedPoints" IS NULL) OR ("IsCorrect" IS NOT NULL AND "AwardedPoints" BETWEEN 0 AND 100)),
                CONSTRAINT "CK_paper_attempt_answers_shape" CHECK (("IsAnswered" = FALSE AND "SelectedOptionId" IS NULL AND "BooleanAnswer" IS NULL AND "TextAnswer" IS NULL AND "NormalizedTextAnswer" IS NULL AND "TextAnswers" IS NULL) OR ("IsAnswered" = TRUE AND (("SelectedOptionId" IS NOT NULL AND "BooleanAnswer" IS NULL AND "TextAnswer" IS NULL AND "NormalizedTextAnswer" IS NULL AND "TextAnswers" IS NULL) OR ("SelectedOptionId" IS NULL AND "BooleanAnswer" IS NOT NULL AND "TextAnswer" IS NULL AND "NormalizedTextAnswer" IS NULL AND "TextAnswers" IS NULL) OR ("SelectedOptionId" IS NULL AND "BooleanAnswer" IS NULL AND "TextAnswer" IS NOT NULL AND "NormalizedTextAnswer" IS NOT NULL AND "TextAnswers" IS NULL) OR ("SelectedOptionId" IS NULL AND "BooleanAnswer" IS NULL AND "TextAnswer" IS NULL AND "NormalizedTextAnswer" IS NULL AND "TextAnswers" IS NOT NULL)))),
                CONSTRAINT "FK_paper_attempt_answers_paper_attempts_AttemptId" FOREIGN KEY ("AttemptId") REFERENCES "paper_attempts" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_paper_attempt_answers_paper_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "paper_questions" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_paper_attempt_answers_paper_question_options_SelectedOptionId" FOREIGN KEY ("SelectedOptionId") REFERENCES "paper_question_options" ("Id") ON DELETE RESTRICT
            );

            CREATE TABLE "paper_category_assignments" (
                "PaperId" uuid NOT NULL,
                "PaperCategoryId" uuid NOT NULL,
                CONSTRAINT "PK_paper_category_assignments" PRIMARY KEY ("PaperId", "PaperCategoryId"),
                CONSTRAINT "FK_paper_category_assignments_papers_PaperId" FOREIGN KEY ("PaperId") REFERENCES "papers" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_paper_category_assignments_paper_categories_PaperCategoryId" FOREIGN KEY ("PaperCategoryId") REFERENCES "paper_categories" ("Id") ON DELETE RESTRICT
            );

            CREATE TABLE "paper_wrong_questions" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "QuestionId" uuid NOT NULL,
                "Status" character varying(20) NOT NULL,
                "WrongCount" integer NOT NULL,
                "RedoCount" integer NOT NULL,
                "FirstWrongAt" timestamp with time zone NOT NULL,
                "LastWrongAt" timestamp with time zone NOT NULL,
                "LastRedoAt" timestamp with time zone,
                "MasteredAt" timestamp with time zone,
                "ConcurrencyStamp" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_paper_wrong_questions" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_paper_wrong_questions_users_UserId" FOREIGN KEY ("UserId") REFERENCES "users" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_paper_wrong_questions_paper_questions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "paper_questions" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX "IX_papers_CreatedById" ON "papers" ("CreatedById");
            CREATE INDEX "IX_papers_LastEditorId" ON "papers" ("LastEditorId");
            CREATE INDEX "IX_papers_Status_PublishedAt_Id" ON "papers" ("Status", "PublishedAt", "Id");
            CREATE INDEX "IX_papers_Status_UpdatedAt_Id" ON "papers" ("Status", "UpdatedAt", "Id");
            CREATE UNIQUE INDEX "IX_paper_categories_Name" ON "paper_categories" ("Name");
            CREATE UNIQUE INDEX "IX_paper_categories_Slug" ON "paper_categories" ("Slug");
            CREATE INDEX "IX_paper_questions_AudioResourceId" ON "paper_questions" ("AudioResourceId");
            CREATE UNIQUE INDEX "IX_paper_questions_PaperId_SortOrder" ON "paper_questions" ("PaperId", "SortOrder");
            CREATE UNIQUE INDEX "IX_fill_blank_accepted_answers_QuestionId_NormalizedText" ON "fill_blank_accepted_answers" ("QuestionId", "NormalizedText");
            CREATE UNIQUE INDEX "IX_fill_blank_accepted_answers_QuestionId_SortOrder" ON "fill_blank_accepted_answers" ("QuestionId", "SortOrder");
            CREATE UNIQUE INDEX "IX_paper_question_options_QuestionId_SortOrder" ON "paper_question_options" ("QuestionId", "SortOrder");
            CREATE UNIQUE INDEX "IX_paper_dictation_blanks_QuestionId_NormalizedAnswer" ON "paper_dictation_blanks" ("QuestionId", "NormalizedAnswer");
            CREATE UNIQUE INDEX "IX_paper_dictation_blanks_QuestionId_SortOrder" ON "paper_dictation_blanks" ("QuestionId", "SortOrder");
            CREATE INDEX "IX_paper_attempts_PaperId" ON "paper_attempts" ("PaperId");
            CREATE UNIQUE INDEX "IX_paper_attempts_UserId_PaperId_AttemptNumber" ON "paper_attempts" ("UserId", "PaperId", "AttemptNumber");
            CREATE UNIQUE INDEX "IX_paper_attempts_UserId_PaperId" ON "paper_attempts" ("UserId", "PaperId") WHERE "Status" = 'InProgress';
            CREATE INDEX "IX_paper_attempts_UserId_StartedAt_Id" ON "paper_attempts" ("UserId", "StartedAt", "Id");
            CREATE INDEX "IX_paper_attempts_UserId_PaperId_StartedAt_Id" ON "paper_attempts" ("UserId", "PaperId", "StartedAt", "Id");
            CREATE UNIQUE INDEX "IX_paper_attempt_answers_AttemptId_QuestionId" ON "paper_attempt_answers" ("AttemptId", "QuestionId");
            CREATE INDEX "IX_paper_attempt_answers_QuestionId" ON "paper_attempt_answers" ("QuestionId");
            CREATE INDEX "IX_paper_attempt_answers_SelectedOptionId" ON "paper_attempt_answers" ("SelectedOptionId");
            CREATE INDEX "IX_paper_category_assignments_PaperCategoryId_PaperId" ON "paper_category_assignments" ("PaperCategoryId", "PaperId");
            CREATE UNIQUE INDEX "IX_paper_wrong_questions_UserId_QuestionId" ON "paper_wrong_questions" ("UserId", "QuestionId");
            CREATE INDEX "IX_paper_wrong_questions_QuestionId" ON "paper_wrong_questions" ("QuestionId");
            CREATE INDEX "IX_paper_wrong_questions_UserId_Status_LastWrongAt_Id" ON "paper_wrong_questions" ("UserId", "Status", "LastWrongAt", "Id");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS
                "paper_attempt_answers",
                "paper_category_assignments",
                "paper_wrong_questions",
                "paper_dictation_blanks",
                "fill_blank_accepted_answers",
                "paper_question_options",
                "paper_attempts",
                "paper_questions",
                "papers",
                "paper_categories"
            CASCADE;
            """);
    }
}
