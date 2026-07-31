using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies article category normalization, visibility and protected deletion behavior.
/// </summary>
public sealed class ArticleCategoryServiceTests
{
    /// <summary>
    /// Verifies category values are normalized before persistence.
    /// </summary>
    [Fact]
    public async Task CreateShouldNormalizeCategoryValues()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var response = await service.CreateAsync(
            new CreateArticleCategoryRequest
            {
                Name = "  Grammar  ",
                Slug = "  BASIC-GRAMMAR  ",
                Description = "  Core lessons  "
            },
            TestContext.Current.CancellationToken);

        response.Name.Should().Be("Grammar");
        response.Slug.Should().Be("basic-grammar");
        response.Description.Should().Be("Core lessons");
        response.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// Verifies public category queries exclude inactive categories.
    /// </summary>
    [Fact]
    public async Task PublicListShouldExcludeInactiveCategories()
    {
        await using var db = CreateDbContext();
        db.ArticleCategories.AddRange(
            new ArticleCategory { Name = "Active", Slug = "active" },
            new ArticleCategory { Name = "Inactive", Slug = "inactive", IsActive = false });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetPublicListAsync(
            new ArticleCategoryListRequest(),
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.Name.Should().Be("Active");
    }

    /// <summary>
    /// Verifies deleting a referenced category returns a conflict without changing article assignments.
    /// </summary>
    [Fact]
    public async Task DeleteShouldRejectCategoryInUseWithoutRemovingAssignments()
    {
        await using var db = CreateDbContext();
        var user = CreateUser();
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        var article = CreateArticle(user, category);
        db.AddRange(user, category, article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.DeleteAsync(
            category.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleCategoryInUse.GetMessage());
        (await db.ArticleCategories.AnyAsync(
            value => value.Id == category.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await db.ArticleCategoryAssignments.AnyAsync(
            value => value.ArticleId == article.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies an unused category can be deleted without changing articles.
    /// </summary>
    [Fact]
    public async Task DeleteShouldRemoveUnusedCategory()
    {
        await using var db = CreateDbContext();
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        db.ArticleCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.DeleteAsync(category.Id, TestContext.Current.CancellationToken);

        (await db.ArticleCategories.AnyAsync(
            value => value.Id == category.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    private static ArticleCategoryService CreateService(ApplicationDbContext db)
        => new(db, new Mock<IDatabaseExceptionClassifier>().Object);

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User CreateUser()
        => new()
        {
            Username = "admin@example.com",
            Email = "admin@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };

    private static Article CreateArticle(User user, ArticleCategory category)
        => new()
        {
            Title = "Article",
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
}
