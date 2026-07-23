using FluentAssertions;
using TinyLang.Dtos;

namespace TinyLang.UnitTests;

public sealed class ArticleValidatorsTests
{
    [Fact]
    public async Task CreateArticleShouldRejectDuplicateOrEmptyMediaIds()
    {
        var duplicateId = Guid.NewGuid();
        var validator = new CreateArticleRequestValidator();
        var request = new CreateArticleRequest
        {
            Title = "Article",
            ContentHtml = "<p>Content</p>",
            MediaResourceIds = [duplicateId, duplicateId, Guid.Empty]
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(request.MediaResourceIds));
    }

    [Fact]
    public async Task CreateArticleShouldAcceptValidDraftWithoutCategory()
    {
        var validator = new CreateArticleRequestValidator();
        var request = new CreateArticleRequest
        {
            Title = "Article",
            Summary = "Summary",
            ContentHtml = "<p>Content</p>",
            MediaResourceIds = []
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ArticleListShouldRejectInvalidPagination(int page, int pageSize)
    {
        var validator = new ArticleListRequestValidator();

        var result = await validator.ValidateAsync(
            new ArticleListRequest { Page = page, PageSize = pageSize },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("with space")]
    [InlineData("double--dash")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    public async Task CategoryShouldRejectInvalidSlug(string slug)
    {
        var validator = new CreateArticleCategoryRequestValidator();

        var result = await validator.ValidateAsync(
            new CreateArticleCategoryRequest { Name = "Category", Slug = slug },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }
}
