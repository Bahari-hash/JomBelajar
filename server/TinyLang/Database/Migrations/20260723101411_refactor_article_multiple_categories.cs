using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class refactor_article_multiple_categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_articles_article_categories_CategoryId",
                table: "articles");

            migrationBuilder.DropIndex(
                name: "IX_articles_CategoryId_Status_PublishedAt",
                table: "articles");

            migrationBuilder.CreateTable(
                name: "article_category_assignments",
                columns: table => new
                {
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleCategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article_category_assignments", x => new { x.ArticleId, x.ArticleCategoryId });
                    table.ForeignKey(
                        name: "FK_article_category_assignments_article_categories_ArticleCate~",
                        column: x => x.ArticleCategoryId,
                        principalTable: "article_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_article_category_assignments_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_article_category_assignments_ArticleCategoryId_ArticleId",
                table: "article_category_assignments",
                columns: new[] { "ArticleCategoryId", "ArticleId" });

            migrationBuilder.Sql(
                "INSERT INTO \"article_category_assignments\" (\"ArticleId\", \"ArticleCategoryId\") " +
                "SELECT \"Id\", \"CategoryId\" FROM \"articles\" WHERE \"CategoryId\" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "articles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "articles",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE \"articles\" AS article SET \"CategoryId\" = assignment.\"ArticleCategoryId\" " +
                "FROM (SELECT DISTINCT ON (\"ArticleId\") \"ArticleId\", \"ArticleCategoryId\" " +
                "FROM \"article_category_assignments\" ORDER BY \"ArticleId\", \"ArticleCategoryId\") AS assignment " +
                "WHERE article.\"Id\" = assignment.\"ArticleId\";");

            migrationBuilder.DropTable(
                name: "article_category_assignments");

            migrationBuilder.CreateIndex(
                name: "IX_articles_CategoryId_Status_PublishedAt",
                table: "articles",
                columns: new[] { "CategoryId", "Status", "PublishedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_articles_article_categories_CategoryId",
                table: "articles",
                column: "CategoryId",
                principalTable: "article_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
