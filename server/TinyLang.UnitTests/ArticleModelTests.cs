using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies EF Core article metadata that cannot be proven by service tests.
/// </summary>
public sealed class ArticleModelTests
{
    /// <summary>
    /// Verifies required Markdown/HTML lengths, optimistic concurrency and protected category deletion metadata.
    /// </summary>
    [Fact]
    public void ModelShouldConfigureMarkdownConcurrencyAndRestrictedCategoryDeletion()
    {
        using var db = CreateDbContext();
        var article = db.Model.FindEntityType(typeof(Article));
        var assignment = db.Model.FindEntityType(typeof(ArticleCategoryAssignment));

        article.Should().NotBeNull();
        article!.FindProperty(nameof(Article.ContentMarkdown))!.IsNullable.Should().BeFalse();
        article.FindProperty(nameof(Article.ContentMarkdown))!.GetMaxLength()
            .Should().Be(ArticleConstraints.MaxContentLength);
        article.FindProperty(nameof(Article.ContentHtml))!.GetMaxLength()
            .Should().Be(ArticleConstraints.MaxContentLength);
        article.FindProperty(nameof(Article.ConcurrencyStamp))!.IsConcurrencyToken.Should().BeTrue();

        var categoryForeignKey = assignment!.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(ArticleCategory));
        categoryForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
