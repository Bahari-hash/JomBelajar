using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperPassingScorePercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_papers_scores",
                table: "papers");

            migrationBuilder.AddColumn<int>(
                name: "PassingScorePercentage",
                table: "papers",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddCheckConstraint(
                name: "CK_papers_scores",
                table: "papers",
                sql: "\"TotalScore\" BETWEEN 0 AND 20000 AND \"PassingScorePercentage\" BETWEEN 1 AND 100 AND \"PassingScore\" BETWEEN 0 AND \"TotalScore\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_papers_scores",
                table: "papers");

            migrationBuilder.DropColumn(
                name: "PassingScorePercentage",
                table: "papers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_papers_scores",
                table: "papers",
                sql: "\"TotalScore\" BETWEEN 0 AND 20000 AND \"PassingScore\" BETWEEN 0 AND \"TotalScore\"");
        }
    }
}
