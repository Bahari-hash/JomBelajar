using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineQuizModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "papers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    LanguageTag = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PassingScore = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_papers", x => x.Id);
                    table.CheckConstraint("CK_papers_scores", "\"TotalScore\" BETWEEN 0 AND 20000 AND \"PassingScore\" BETWEEN 0 AND \"TotalScore\"");
                    table.ForeignKey(
                        name: "FK_papers_users_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_papers_users_LastEditorId",
                        column: x => x.LastEditorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "paper_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaperTotalScore = table.Column<int>(type: "integer", nullable: false),
                    PaperPassingScore = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    IsPassed = table.Column<bool>(type: "boolean", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_attempts", x => x.Id);
                    table.CheckConstraint("CK_paper_attempts_number", "\"AttemptNumber\" >= 1");
                    table.CheckConstraint("CK_paper_attempts_snapshot_scores", "\"PaperTotalScore\" BETWEEN 0 AND 20000 AND \"PaperPassingScore\" BETWEEN 0 AND \"PaperTotalScore\"");
                    table.CheckConstraint("CK_paper_attempts_submission_state", "(\"Status\" = 'InProgress' AND \"Score\" IS NULL AND \"IsPassed\" IS NULL AND \"SubmittedAt\" IS NULL) OR (\"Status\" = 'Submitted' AND \"Score\" IS NOT NULL AND \"Score\" BETWEEN 0 AND \"PaperTotalScore\" AND \"IsPassed\" IS NOT NULL AND \"SubmittedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_paper_attempts_papers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_paper_attempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Prompt = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CorrectBoolean = table.Column<bool>(type: "boolean", nullable: true),
                    FillBlankCaseSensitive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_questions", x => x.Id);
                    table.CheckConstraint("CK_paper_questions_points", "\"Points\" BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_paper_questions_sort_order", "\"SortOrder\" BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_paper_questions_type_fields", "(\"Type\" = 'TrueFalse' OR \"CorrectBoolean\" IS NULL) AND (\"Type\" = 'FillBlank' OR \"FillBlankCaseSensitive\" = FALSE)");
                    table.ForeignKey(
                        name: "FK_paper_questions_papers_PaperId",
                        column: x => x.PaperId,
                        principalTable: "papers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fill_blank_accepted_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NormalizedText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fill_blank_accepted_answers", x => x.Id);
                    table.CheckConstraint("CK_fill_blank_accepted_answers_sort_order", "\"SortOrder\" BETWEEN 0 AND 10000");
                    table.ForeignKey(
                        name: "FK_fill_blank_accepted_answers_paper_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "paper_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_question_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_question_options", x => x.Id);
                    table.CheckConstraint("CK_paper_question_options_sort_order", "\"SortOrder\" BETWEEN 0 AND 10000");
                    table.ForeignKey(
                        name: "FK_paper_question_options_paper_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "paper_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paper_attempt_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BooleanAnswer = table.Column<bool>(type: "boolean", nullable: true),
                    TextAnswer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NormalizedTextAnswer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsAnswered = table.Column<bool>(type: "boolean", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    AwardedPoints = table.Column<int>(type: "integer", nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_paper_attempt_answers", x => x.Id);
                    table.CheckConstraint("CK_paper_attempt_answers_awarded_points", "(\"IsCorrect\" IS NULL AND \"AwardedPoints\" IS NULL) OR (\"IsCorrect\" IS NOT NULL AND \"AwardedPoints\" BETWEEN 0 AND 100)");
                    table.CheckConstraint("CK_paper_attempt_answers_shape", "(\"IsAnswered\" = FALSE AND \"SelectedOptionId\" IS NULL AND \"BooleanAnswer\" IS NULL AND \"TextAnswer\" IS NULL AND \"NormalizedTextAnswer\" IS NULL) OR (\"IsAnswered\" = TRUE AND ((\"SelectedOptionId\" IS NOT NULL AND \"BooleanAnswer\" IS NULL AND \"TextAnswer\" IS NULL AND \"NormalizedTextAnswer\" IS NULL) OR (\"SelectedOptionId\" IS NULL AND \"BooleanAnswer\" IS NOT NULL AND \"TextAnswer\" IS NULL AND \"NormalizedTextAnswer\" IS NULL) OR (\"SelectedOptionId\" IS NULL AND \"BooleanAnswer\" IS NULL AND \"TextAnswer\" IS NOT NULL AND \"NormalizedTextAnswer\" IS NOT NULL)))");
                    table.ForeignKey(
                        name: "FK_paper_attempt_answers_paper_attempts_AttemptId",
                        column: x => x.AttemptId,
                        principalTable: "paper_attempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_paper_attempt_answers_paper_question_options_SelectedOption~",
                        column: x => x.SelectedOptionId,
                        principalTable: "paper_question_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_paper_attempt_answers_paper_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "paper_questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fill_blank_accepted_answers_QuestionId_NormalizedText",
                table: "fill_blank_accepted_answers",
                columns: new[] { "QuestionId", "NormalizedText" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fill_blank_accepted_answers_QuestionId_SortOrder",
                table: "fill_blank_accepted_answers",
                columns: new[] { "QuestionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempt_answers_AttemptId_QuestionId",
                table: "paper_attempt_answers",
                columns: new[] { "AttemptId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempt_answers_QuestionId",
                table: "paper_attempt_answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempt_answers_SelectedOptionId",
                table: "paper_attempt_answers",
                column: "SelectedOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempts_PaperId",
                table: "paper_attempts",
                column: "PaperId");

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempts_UserId_PaperId",
                table: "paper_attempts",
                columns: new[] { "UserId", "PaperId" },
                unique: true,
                filter: "\"Status\" = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempts_UserId_PaperId_AttemptNumber",
                table: "paper_attempts",
                columns: new[] { "UserId", "PaperId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempts_UserId_PaperId_StartedAt_Id",
                table: "paper_attempts",
                columns: new[] { "UserId", "PaperId", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_paper_attempts_UserId_StartedAt_Id",
                table: "paper_attempts",
                columns: new[] { "UserId", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_paper_question_options_QuestionId_SortOrder",
                table: "paper_question_options",
                columns: new[] { "QuestionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_paper_questions_PaperId_SortOrder",
                table: "paper_questions",
                columns: new[] { "PaperId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_papers_CreatedById",
                table: "papers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_papers_LastEditorId",
                table: "papers",
                column: "LastEditorId");

            migrationBuilder.CreateIndex(
                name: "IX_papers_Status_LanguageTag_PublishedAt_Id",
                table: "papers",
                columns: new[] { "Status", "LanguageTag", "PublishedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_papers_Status_UpdatedAt_Id",
                table: "papers",
                columns: new[] { "Status", "UpdatedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fill_blank_accepted_answers");

            migrationBuilder.DropTable(
                name: "paper_attempt_answers");

            migrationBuilder.DropTable(
                name: "paper_attempts");

            migrationBuilder.DropTable(
                name: "paper_question_options");

            migrationBuilder.DropTable(
                name: "paper_questions");

            migrationBuilder.DropTable(
                name: "papers");
        }
    }
}
