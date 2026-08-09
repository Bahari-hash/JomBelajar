using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyWordStudySettingsAndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StudyDateUtc",
                table: "word_study_sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "DailyWordStudyCount",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.CreateIndex(
                name: "IX_word_study_sessions_UserId_StudyDateUtc",
                table: "word_study_sessions",
                columns: new[] { "UserId", "StudyDateUtc" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_daily_word_study_count",
                table: "users",
                sql: "\"DailyWordStudyCount\" BETWEEN 1 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_word_study_sessions_UserId_StudyDateUtc",
                table: "word_study_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_users_daily_word_study_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "StudyDateUtc",
                table: "word_study_sessions");

            migrationBuilder.DropColumn(
                name: "DailyWordStudyCount",
                table: "users");
        }
    }
}
