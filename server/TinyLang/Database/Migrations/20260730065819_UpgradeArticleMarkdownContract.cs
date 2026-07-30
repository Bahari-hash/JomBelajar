using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeArticleMarkdownContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_article_category_assignments_article_categories_ArticleCate~",
                table: "article_category_assignments");

            migrationBuilder.Sql("DELETE FROM \"articles\";");

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "articles",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "ContentMarkdown",
                table: "articles",
                type: "character varying(1000000)",
                maxLength: 1000000,
                nullable: false);

            migrationBuilder.AddForeignKey(
                name: "FK_article_category_assignments_article_categories",
                table: "article_category_assignments",
                column: "ArticleCategoryId",
                principalTable: "article_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_article_category_assignments_article_categories",
                table: "article_category_assignments");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "ContentMarkdown",
                table: "articles");

            migrationBuilder.AddForeignKey(
                name: "FK_article_category_assignments_article_categories_ArticleCate~",
                table: "article_category_assignments",
                column: "ArticleCategoryId",
                principalTable: "article_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
