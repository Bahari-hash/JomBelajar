using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
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
            new ArticleCategoryListRequest { IncludeInactive = true },
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.Name.Should().Be("Active");
    }

    [Fact]
    public async Task DeactivateShouldBeIdempotent()
    {
        await using var db = CreateDbContext();
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        db.ArticleCategories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.DeactivateAsync(category.Id, TestContext.Current.CancellationToken);
        await service.DeactivateAsync(category.Id, TestContext.Current.CancellationToken);

        category.IsActive.Should().BeFalse();
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
}
