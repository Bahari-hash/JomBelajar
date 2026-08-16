using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies article and category HTTP request validation contracts.
/// </summary>
public sealed class ArticleValidatorsTests
{
    /// <summary>
    /// Verifies body media identifiers must be unique and non-empty.
    /// </summary>
    [Fact]
    public async Task CreateArticleShouldRejectDuplicateOrEmptyBodyMediaIds()
    {
        var duplicateId = Guid.NewGuid();
        var request = new CreateArticleRequest
        {
            Title = "Article",
            ContentMarkdown = "Content",
            BodyMediaResourceIds = [duplicateId, duplicateId, Guid.Empty]
        };

        var result = await new CreateArticleRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.BodyMediaResourceIds));
    }

    /// <summary>
    /// Verifies a valid Markdown draft can remain uncategorized.
    /// </summary>
    [Fact]
    public async Task CreateArticleShouldAcceptValidMarkdownDraftWithoutCategory()
    {
        var request = new CreateArticleRequest
        {
            Title = "Article",
            ContentMarkdown = "# Content"
        };

        var result = await new CreateArticleRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Verifies an empty reading audio identifier is rejected with the stable article error code.
    /// </summary>
    [Fact]
    public async Task CreateArticleShouldRejectEmptyReadingAudioId()
    {
        var request = new CreateArticleRequest
        {
            Title = "Article",
            ContentMarkdown = "Content",
            ReadingAudioResourceId = Guid.Empty
        };

        var result = await new CreateArticleRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(request.ReadingAudioResourceId) &&
            error.ErrorCode == ErrorCodes.ArticleReadingAudioInvalid.ToString());
    }

    /// <summary>
    /// Verifies a non-empty reading audio identifier is accepted on create requests.
    /// </summary>
    [Fact]
    public async Task CreateArticleShouldAcceptNonEmptyReadingAudioId()
    {
        var request = new CreateArticleRequest
        {
            Title = "Article",
            ContentMarkdown = "Content",
            ReadingAudioResourceId = Guid.NewGuid()
        };

        var result = await new CreateArticleRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// Verifies update requests reuse the reading audio identifier rule.
    /// </summary>
    [Fact]
    public async Task UpdateArticleShouldRejectEmptyReadingAudioId()
    {
        var request = new UpdateArticleRequest
        {
            Title = "Article",
            ContentMarkdown = "Content",
            ConcurrencyStamp = Guid.NewGuid(),
            ReadingAudioResourceId = Guid.Empty
        };

        var result = await new UpdateArticleRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(request.ReadingAudioResourceId) &&
            error.ErrorCode == ErrorCodes.ArticleReadingAudioInvalid.ToString());
    }

    /// <summary>
    /// Verifies update requests require a non-empty concurrency stamp.
    /// </summary>
    [Fact]
    public async Task UpdateArticleShouldRequireConcurrencyStamp()
    {
        var request = new UpdateArticleRequest
        {
            Title = "Article",
            ContentMarkdown = "Content",
            ConcurrencyStamp = Guid.Empty
        };

        var result = await new UpdateArticleRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.Errors.Should().Contain(
            error => error.PropertyName == nameof(request.ConcurrencyStamp));
    }

    /// <summary>
    /// Verifies preview Markdown uses the same required and length limits as saved articles.
    /// </summary>
    [Fact]
    public async Task PreviewShouldRejectOversizedMarkdown()
    {
        var request = new ArticlePreviewRequest
        {
            ContentMarkdown = new string('a', ArticleConstraints.MaxContentLength + 1)
        };

        var result = await new ArticlePreviewRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Verifies duplicate category identifiers are rejected.
    /// </summary>
    [Fact]
    public async Task CreateArticleShouldRejectDuplicateCategories()
    {
        var categoryId = Guid.NewGuid();
        var result = await new CreateArticleRequestValidator().ValidateAsync(
            new CreateArticleRequest
            {
                Title = "Article",
                ContentMarkdown = "Content",
                CategoryIds = [categoryId, categoryId]
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Verifies invalid article list pagination is rejected.
    /// </summary>
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    public async Task ArticleListShouldRejectInvalidPagination(int page, int pageSize)
    {
        var result = await new ArticleListRequestValidator().ValidateAsync(
            new ArticleListRequest { Page = page, PageSize = pageSize },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// Verifies category slugs follow the stable URL-safe format.
    /// </summary>
    [Theory]
    [InlineData("two--hyphens")]
    [InlineData(" leading")]
    [InlineData("under_score")]
    public async Task CategoryShouldRejectInvalidSlug(string slug)
    {
        var result = await new CreateArticleCategoryRequestValidator().ValidateAsync(
            new CreateArticleCategoryRequest { Name = "Category", Slug = slug },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }
}
