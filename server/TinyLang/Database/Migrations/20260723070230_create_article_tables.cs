using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class create_article_tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "article_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "articles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentHtml = table.Column<string>(type: "character varying(1000000)", maxLength: 1000000, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedById = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CoverMediaResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_articles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_articles_article_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "article_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_articles_media_resources_CoverMediaResourceId",
                        column: x => x.CoverMediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_articles_users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_articles_users_LastEditorId",
                        column: x => x.LastEditorId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_articles_users_PublishedById",
                        column: x => x.PublishedById,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "article_media_resources",
                columns: table => new
                {
                    ArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaResourceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_article_media_resources", x => new { x.ArticleId, x.MediaResourceId });
                    table.ForeignKey(
                        name: "FK_article_media_resources_articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_article_media_resources_media_resources_MediaResourceId",
                        column: x => x.MediaResourceId,
                        principalTable: "media_resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_article_categories_Name",
                table: "article_categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_article_categories_Slug",
                table: "article_categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_article_media_resources_MediaResourceId",
                table: "article_media_resources",
                column: "MediaResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_articles_AuthorId_CreatedAt",
                table: "articles",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_articles_CategoryId_Status_PublishedAt",
                table: "articles",
                columns: new[] { "CategoryId", "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_articles_CoverMediaResourceId",
                table: "articles",
                column: "CoverMediaResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_articles_LastEditorId_UpdatedAt",
                table: "articles",
                columns: new[] { "LastEditorId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_articles_PublishedById",
                table: "articles",
                column: "PublishedById");

            migrationBuilder.CreateIndex(
                name: "IX_articles_Status_PublishedAt",
                table: "articles",
                columns: new[] { "Status", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "article_media_resources");

            migrationBuilder.DropTable(
                name: "articles");

            migrationBuilder.DropTable(
                name: "article_categories");
        }
    }
}
