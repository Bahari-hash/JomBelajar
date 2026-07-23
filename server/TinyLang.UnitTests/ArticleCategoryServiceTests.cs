using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class ArticleCategoryServiceTests
{
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

    [Fact]
    public async Task DeleteShouldClearAssignmentsAndKeepArticle()
    {
        await using var db = CreateDbContext();
        var user = new ArticleServiceTestsUserFactory().Create();
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        var article = new Article
        {
            Title = "Article",
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
        db.AddRange(user, category, article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.DeleteAsync(category.Id, TestContext.Current.CancellationToken);

        (await db.ArticleCategories.AnyAsync(x => x.Id == category.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse();
        (await db.ArticleCategoryAssignments.AnyAsync(
            x => x.ArticleId == article.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await db.Articles.AnyAsync(x => x.Id == article.Id, TestContext.Current.CancellationToken))
            .Should().BeTrue();
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

    private sealed class ArticleServiceTestsUserFactory
    {
        public User Create() => new()
        {
            Username = "editor@example.com",
            Email = "editor@example.com",
            PasswordHash = "hash",
            Role = UserRole.Editor
        };
    }
}
