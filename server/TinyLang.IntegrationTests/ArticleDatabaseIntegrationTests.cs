using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.IntegrationTests;

/// <summary>
/// Verifies destructive article migration, category association operations and concurrency against PostgreSQL.
/// </summary>
public sealed class ArticleDatabaseIntegrationTests
{
    private const string PreviousMigration = "20260729160826_ImproveUserManagementQueries";

    /// <summary>
    /// Migrates seeded legacy article data and verifies only article-owned data is reset before new invariants apply.
    /// </summary>
    [Fact]
    public async Task ArticleSchemaShouldResetLegacyRowsAndEnforceNewDatabaseBehavior()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL article tests.");
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var schema = $"tiny_lang_article_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync(cancellationToken);
        await ExecuteSchemaCommandAsync(
            adminConnection,
            $"CREATE SCHEMA \"{schema}\"",
            cancellationToken);

        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(schemaConnection)
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;

            Guid categoryId;
            Guid mediaId;
            await using (var db = new ApplicationDbContext(options))
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration, cancellationToken);
                (categoryId, mediaId) = await SeedLegacyArticleAsync(db, cancellationToken);

                await migrator.MigrateAsync(cancellationToken: cancellationToken);
                db.ClearTrackedChanges();

                (await db.Articles.CountAsync(cancellationToken)).Should().Be(0);
                (await db.ArticleCategoryAssignments.CountAsync(cancellationToken)).Should().Be(0);
                (await db.ArticleMediaResources.CountAsync(cancellationToken)).Should().Be(0);
                (await db.ArticleCategories.AnyAsync(
                    value => value.Id == categoryId,
                    cancellationToken)).Should().BeTrue();
                (await db.MediaResources.AnyAsync(
                    value => value.Id == mediaId,
                    cancellationToken)).Should().BeTrue();
            }

            Guid articleId;
            await using (var db = new ApplicationDbContext(options))
            {
                var user = await db.Users.FirstAsync(cancellationToken);
                var category = await db.ArticleCategories.SingleAsync(
                    value => value.Id == categoryId,
                    cancellationToken);
                var article = CreateCurrentArticle(user, category);
                db.Articles.Add(article);
                await db.SaveChangesAsync(cancellationToken);
                articleId = article.Id;

                var categoryService = new ArticleCategoryService(
                    db,
                    new PostgresDatabaseExceptionClassifier());
                var cleared = await categoryService.ClearArticlesAsync(
                    categoryId,
                    cancellationToken);
                var repeated = await categoryService.ClearArticlesAsync(
                    categoryId,
                    cancellationToken);

                cleared.RemovedArticleCount.Should().Be(1);
                repeated.RemovedArticleCount.Should().Be(0);
                (await db.Articles.AnyAsync(
                    value => value.Id == articleId,
                    cancellationToken)).Should().BeTrue();
                db.ClearTrackedChanges();
                await categoryService.DeleteAsync(categoryId, cancellationToken);
            }

            await VerifyRestrictedCategoryDeleteAsync(options, cancellationToken);
            await VerifyArticleConcurrencyAsync(options, articleId, cancellationToken);
        }
        finally
        {
            await ExecuteSchemaCommandAsync(
                adminConnection,
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                cancellationToken);
        }
    }

    /// <summary>
    /// Seeds the previous schema with HTML-only article data and both category and media joins.
    /// </summary>
    private static async Task<(Guid CategoryId, Guid MediaId)> SeedLegacyArticleAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = $"editor-{Guid.NewGuid():N}",
            Email = $"editor-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Editor
        };
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        var media = new MediaResource
        {
            Uploader = user,
            UploaderId = user.Id,
            ObjectName = $"article_pictures/{Guid.NewGuid():N}.png",
            OriginalName = "legacy.png",
            Module = ResourceModule.ArticlePicture,
            Status = ResourceStatus.Active,
            Size = 100,
            Extension = ".png",
            ContentType = "image/png",
            Url = $"https://cdn.example.com/{Guid.NewGuid():N}.png"
        };
        db.AddRange(user, category, media);
        await db.SaveChangesAsync(cancellationToken);

        var articleId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "articles"
                ("Id", "Title", "Summary", "ContentHtml", "Status", "AuthorId",
                 "LastEditorId", "PublishedById", "PublishedAt", "CoverMediaResourceId",
                 "CreatedAt", "UpdatedAt")
            VALUES
                ({articleId}, {"Legacy"}, NULL, {"<p>Legacy</p>"}, {"Draft"}, {user.Id},
                 {user.Id}, NULL, NULL, {media.Id}, {timestamp}, {timestamp});
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "article_category_assignments" ("ArticleId", "ArticleCategoryId")
            VALUES ({articleId}, {category.Id});
            """, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "article_media_resources" ("ArticleId", "MediaResourceId")
            VALUES ({articleId}, {media.Id});
            """, cancellationToken);

        return (category.Id, media.Id);
    }

    /// <summary>
    /// Verifies the database rejects direct deletion of a category that gained an assignment.
    /// </summary>
    private static async Task VerifyRestrictedCategoryDeleteAsync(
        DbContextOptions<ApplicationDbContext> options,
        CancellationToken cancellationToken)
    {
        await using var setupDb = new ApplicationDbContext(options);
        var article = await setupDb.Articles.FirstAsync(cancellationToken);
        var category = new ArticleCategory { Name = "Restricted", Slug = "restricted" };
        category.ArticleAssignments.Add(new ArticleCategoryAssignment
        {
            ArticleId = article.Id,
            Article = article
        });
        setupDb.ArticleCategories.Add(category);
        await setupDb.SaveChangesAsync(cancellationToken);

        await using var deleteDb = new ApplicationDbContext(options);
        var referenced = await deleteDb.ArticleCategories.SingleAsync(
            value => value.Id == category.Id,
            cancellationToken);
        deleteDb.ArticleCategories.Remove(referenced);

        var action = async () => await deleteDb.SaveChangesAsync(cancellationToken);
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// Verifies two contexts cannot save changes using the same original article concurrency token.
    /// </summary>
    private static async Task VerifyArticleConcurrencyAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var first = await firstDb.Articles.SingleAsync(
            value => value.Id == articleId,
            cancellationToken);
        var second = await secondDb.Articles.SingleAsync(
            value => value.Id == articleId,
            cancellationToken);
        first.Title = "First";
        first.ConcurrencyStamp = Guid.NewGuid();
        second.Title = "Second";
        second.ConcurrencyStamp = Guid.NewGuid();
        await firstDb.SaveChangesAsync(cancellationToken);

        var action = async () => await secondDb.SaveChangesAsync(cancellationToken);
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// Creates a valid current-model draft associated with one category.
    /// </summary>
    private static Article CreateCurrentArticle(User user, ArticleCategory category)
        => new()
        {
            Title = "Current",
            ContentMarkdown = "Body",
            ContentHtml = "<p>Body</p>",
            Author = user,
            AuthorId = user.Id,
            LastEditor = user,
            LastEditorId = user.Id,
            CategoryAssignments =
            {
                new ArticleCategoryAssignment
                {
                    ArticleCategory = category,
                    ArticleCategoryId = category.Id
                }
            }
        };

    /// <summary>
    /// Executes schema setup and cleanup commands against the administrative connection.
    /// </summary>
    private static async Task ExecuteSchemaCommandAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
