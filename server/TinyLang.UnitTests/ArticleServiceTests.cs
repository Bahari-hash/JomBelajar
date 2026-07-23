using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class ArticleServiceTests
{
    [Fact]
    public async Task CreateDraftShouldSanitizeContentAndAssociateOwnedMedia()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var media = CreateMedia(editor, ResourceStatus.Active);
        db.AddRange(editor, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var response = await service.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest
            {
                Title = "  Safe article  ",
                ContentHtml = $"<p onclick=\"alert(1)\">Body</p><img src=\"{media.Url}\">",
                MediaResourceIds = [media.Id]
            },
            TestContext.Current.CancellationToken);

        response.Title.Should().Be("Safe article");
        response.ContentHtml.Should().NotContain("onclick");
        response.MediaResourceIds.Should().ContainSingle().Which.Should().Be(media.Id);
        (await db.ArticleMediaResources.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task CreateDraftShouldRejectMediaUploadedByAnotherUser()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var owner = CreateUser("owner@example.com");
        var media = CreateMedia(owner, ResourceStatus.Active);
        db.AddRange(editor, owner, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var action = () => service.CreateDraftAsync(
            editor.Id,
            CreateRequestWithMedia(media),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ForbiddenException>()
            .WithMessage(ErrorCodes.ArticleMediaOwnershipMismatch.GetMessage());
    }

    [Fact]
    public async Task CreateDraftShouldRejectPendingMedia()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var media = CreateMedia(editor, ResourceStatus.Pending);
        db.AddRange(editor, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var action = () => service.CreateDraftAsync(
            editor.Id,
            CreateRequestWithMedia(media),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleMediaNotConfirmed.GetMessage());
    }

    [Fact]
    public async Task CreateDraftShouldRejectUndeclaredBodyImage()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var media = CreateMedia(editor, ResourceStatus.Active);
        db.AddRange(editor, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());
        var request = CreateRequestWithMedia(media) with { MediaResourceIds = [] };

        var action = () => service.CreateDraftAsync(
            editor.Id,
            request,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleMediaNotReferenced.GetMessage());
    }

    [Fact]
    public async Task UpdateShouldAllowAnotherEditorToReuseMediaAlreadyLinkedToTheArticle()
    {
        await using var db = CreateDbContext();
        var author = CreateUser("author@example.com");
        var editor = CreateUser("editor@example.com");
        var media = CreateMedia(author, ResourceStatus.Active);
        db.AddRange(author, editor, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(author, category: null);
        article.MediaResources.Add(new ArticleMediaResource { MediaResourceId = media.Id });
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var response = await service.UpdateAsync(
            article.Id,
            editor.Id,
            new UpdateArticleRequest
            {
                Title = "Updated",
                ContentHtml = $"<p>Body</p><img src=\"{media.Url}\">",
                MediaResourceIds = [media.Id]
            },
            TestContext.Current.CancellationToken);

        response.LastEditor!.Id.Should().Be(editor.Id);
        response.MediaResourceIds.Should().ContainSingle().Which.Should().Be(media.Id);
    }

    [Fact]
    public async Task CoverMediaShouldBeAssociatedWithoutAppearingInBodyHtml()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var cover = CreateMedia(editor, ResourceStatus.Active);
        db.AddRange(editor, cover);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var response = await service.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest
            {
                Title = "Article",
                ContentHtml = "<p>Body</p>",
                CoverMediaResourceId = cover.Id
            },
            TestContext.Current.CancellationToken);

        response.CoverUrl.Should().Be(cover.Url);
        response.MediaResourceIds.Should().ContainSingle().Which.Should().Be(cover.Id);
    }

    [Fact]
    public async Task PublishShouldRequireAnActiveCategory()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar", IsActive = false };
        db.AddRange(editor, category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor, category);
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var action = () => service.PublishAsync(
            article.Id,
            editor.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleCategoryInactive.GetMessage());
    }

    [Fact]
    public async Task PublishAndUnpublishShouldApplyTheStatusMachine()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        db.AddRange(editor, category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor, category);
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var published = await service.PublishAsync(
            article.Id,
            editor.Id,
            TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            article.Id,
            editor.Id,
            TestContext.Current.CancellationToken);

        published.Status.Should().Be(ArticleStatus.Published);
        published.PublishedAt.Should().NotBeNull();
        unpublished.Status.Should().Be(ArticleStatus.Draft);
        unpublished.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task PublicQueriesShouldNeverExposeDraftsOrArchivedArticles()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var category = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        db.AddRange(editor, category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var published = CreateArticle(editor, category, ArticleStatus.Published);
        var draft = CreateArticle(editor, category, ArticleStatus.Draft);
        var archived = CreateArticle(editor, category, ArticleStatus.Archived);
        db.Articles.AddRange(published, draft, archived);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var response = await service.GetPublicListAsync(
            new ArticleListRequest(),
            TestContext.Current.CancellationToken);
        var hiddenDetail = () => service.GetPublicByIdAsync(
            draft.Id,
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.Id.Should().Be(published.Id);
        await hiddenDetail.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ArchivedArticleShouldNotBeEditable()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor, category: null, ArticleStatus.Archived);
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ArticleService(db, new HtmlContentSanitizer());

        var action = () => service.UpdateAsync(
            article.Id,
            editor.Id,
            new UpdateArticleRequest { Title = "Changed", ContentHtml = "<p>Changed</p>" },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleStatusConflict.GetMessage());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static User CreateUser(string email)
        => new()
        {
            Username = email,
            Email = email,
            PasswordHash = "hash",
            Role = UserRole.Editor
        };

    private static MediaResource CreateMedia(User uploader, ResourceStatus status)
        => new()
        {
            Uploader = uploader,
            UploaderId = uploader.Id,
            ObjectName = $"article_pictures/{Guid.NewGuid():N}.png",
            OriginalName = "image.png",
            Module = ResourceModule.ArticlePicture,
            Status = status,
            Size = 100,
            Extension = ".png",
            ContentType = "image/png",
            Url = $"https://cdn.example.com/{Guid.NewGuid():N}.png"
        };

    private static CreateArticleRequest CreateRequestWithMedia(MediaResource media)
        => new()
        {
            Title = "Article",
            ContentHtml = $"<p>Body</p><img src=\"{media.Url}\">",
            MediaResourceIds = [media.Id]
        };

    private static Article CreateArticle(
        User editor,
        ArticleCategory? category,
        ArticleStatus status = ArticleStatus.Draft)
        => new()
        {
            Title = $"Article {Guid.NewGuid():N}",
            ContentHtml = "<p>Body</p>",
            Status = status,
            Category = category,
            CategoryId = category?.Id,
            Author = editor,
            AuthorId = editor.Id,
            LastEditor = editor,
            LastEditorId = editor.Id,
            PublishedBy = status == ArticleStatus.Published ? editor : null,
            PublishedById = status == ArticleStatus.Published ? editor.Id : null,
            PublishedAt = status == ArticleStatus.Published ? DateTimeOffset.UtcNow : null
        };
}
