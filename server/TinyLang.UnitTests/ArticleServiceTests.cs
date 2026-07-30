using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies article Markdown, media, concurrency, category and publication business rules.
/// </summary>
public sealed class ArticleServiceTests
{
    /// <summary>
    /// Verifies create stores the exact Markdown source, derives HTML and supports multiple or no categories.
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldRoundTripMarkdownAndSupportOptionalCategories()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var grammar = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        var listening = new ArticleCategory { Name = "Listening", Slug = "listening" };
        db.AddRange(editor, grammar, listening);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        const string markdown = "# Heading\n\nBody with **emphasis**.";

        var categorized = await service.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest
            {
                Title = "  Categorized  ",
                ContentMarkdown = markdown,
                CategoryIds = [grammar.Id, listening.Id]
            },
            TestContext.Current.CancellationToken);
        var uncategorized = await service.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest { Title = "Uncategorized", ContentMarkdown = "Body" },
            TestContext.Current.CancellationToken);

        categorized.Title.Should().Be("Categorized");
        categorized.ContentMarkdown.Should().Be(markdown);
        categorized.ContentHtml.Should().Contain("<h1>Heading</h1>");
        categorized.Categories.Select(value => value.Id)
            .Should().BeEquivalentTo([grammar.Id, listening.Id]);
        categorized.ConcurrencyStamp.Should().NotBe(Guid.Empty);
        uncategorized.Categories.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies preview uses the same canonical renderer without persisting an article.
    /// </summary>
    [Fact]
    public async Task PreviewShouldRenderWithoutPersistingArticle()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var response = await service.PreviewAsync(
            new ArticlePreviewRequest { ContentMarkdown = "# Preview" },
            TestContext.Current.CancellationToken);

        response.ContentHtml.Should().Contain("<h1>Preview</h1>");
        (await db.Articles.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// Verifies update replaces categories and returns a new concurrency stamp.
    /// </summary>
    [Fact]
    public async Task UpdateShouldReplaceCategoriesAndAdvanceConcurrencyStamp()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var first = new ArticleCategory { Name = "Grammar", Slug = "grammar" };
        var second = new ArticleCategory { Name = "Listening", Slug = "listening" };
        db.AddRange(editor, first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor, first);
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var originalStamp = article.ConcurrencyStamp;

        var response = await service.UpdateAsync(
            article.Id,
            editor.Id,
            new UpdateArticleRequest
            {
                Title = "Updated",
                ContentMarkdown = "Updated body",
                CategoryIds = [second.Id],
                ConcurrencyStamp = originalStamp
            },
            TestContext.Current.CancellationToken);

        response.Categories.Should().ContainSingle().Which.Id.Should().Be(second.Id);
        response.ConcurrencyStamp.Should().NotBe(originalStamp);
        response.ContentHtml.Should().Contain("Updated body");
        (await db.ArticleCategoryAssignments.CountAsync(
            value => value.ArticleId == article.Id,
            TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>
    /// Verifies stale updates fail before overwriting current article content.
    /// </summary>
    [Fact]
    public async Task UpdateShouldRejectStaleConcurrencyStamp()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor);
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.UpdateAsync(
            article.Id,
            editor.Id,
            new UpdateArticleRequest
            {
                Title = "Stale",
                ContentMarkdown = "Stale body",
                ConcurrencyStamp = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleConcurrencyConflict.GetMessage());
    }

    /// <summary>
    /// Verifies cover-only articles do not create or return body media associations.
    /// </summary>
    [Fact]
    public async Task CoverOnlyDraftShouldKeepCoverSeparateFromBodyMedia()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var cover = CreateMedia(editor, ResourceStatus.Active);
        db.AddRange(editor, cover);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest
            {
                Title = "Article",
                ContentMarkdown = "Body",
                CoverMediaResourceId = cover.Id
            },
            TestContext.Current.CancellationToken);

        response.CoverMedia.Should().NotBeNull();
        response.CoverMedia!.Id.Should().Be(cover.Id);
        response.BodyMedia.Should().BeEmpty();
        (await db.ArticleMediaResources.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// Verifies one managed image may be both cover and body media and round-trip unchanged.
    /// </summary>
    [Fact]
    public async Task CoverAndBodyMediaShouldRoundTripThroughUnchangedUpdate()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var image = CreateMedia(editor, ResourceStatus.Active);
        db.AddRange(editor, image);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var markdown = $"Body\n\n![Lesson]({image.Url})";
        var created = await service.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest
            {
                Title = "Article",
                ContentMarkdown = markdown,
                CoverMediaResourceId = image.Id,
                BodyMediaResourceIds = [image.Id]
            },
            TestContext.Current.CancellationToken);

        var updated = await service.UpdateAsync(
            created.Id,
            editor.Id,
            new UpdateArticleRequest
            {
                Title = created.Title,
                Summary = created.Summary,
                ContentMarkdown = created.ContentMarkdown,
                CategoryIds = created.Categories.Select(value => value.Id).ToArray(),
                CoverMediaResourceId = created.CoverMedia?.Id,
                BodyMediaResourceIds = created.BodyMedia.Select(value => value.Id).ToArray(),
                ConcurrencyStamp = created.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        updated.CoverMedia!.Id.Should().Be(image.Id);
        updated.BodyMedia.Should().ContainSingle().Which.Id.Should().Be(image.Id);
        updated.ContentMarkdown.Should().Be(markdown);
        (await db.ArticleMediaResources.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>
    /// Verifies removing a Markdown image also removes its body association.
    /// </summary>
    [Fact]
    public async Task UpdateShouldRemoveBodyMediaNoLongerReferencedByMarkdown()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var image = CreateMedia(editor, ResourceStatus.Active);
        db.AddRange(editor, image);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            editor.Id,
            CreateRequestWithMedia(image),
            TestContext.Current.CancellationToken);

        var updated = await service.UpdateAsync(
            created.Id,
            editor.Id,
            new UpdateArticleRequest
            {
                Title = "Updated",
                ContentMarkdown = "Body without image",
                ConcurrencyStamp = created.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        updated.BodyMedia.Should().BeEmpty();
        (await db.ArticleMediaResources.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// Verifies a new media resource owned by another editor cannot be attached.
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldRejectMediaUploadedByAnotherUser()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var owner = CreateUser("owner@example.com");
        var media = CreateMedia(owner, ResourceStatus.Active);
        db.AddRange(editor, owner, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateDraftAsync(
            editor.Id,
            CreateRequestWithMedia(media),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ForbiddenException>()
            .WithMessage(ErrorCodes.ArticleMediaOwnershipMismatch.GetMessage());
    }

    /// <summary>
    /// Verifies another editor can retain a managed resource already associated with the article.
    /// </summary>
    [Fact]
    public async Task UpdateShouldAllowAnotherEditorToReuseExistingBodyMedia()
    {
        await using var db = CreateDbContext();
        var author = CreateUser("author@example.com");
        var editor = CreateUser("editor@example.com");
        var media = CreateMedia(author, ResourceStatus.Active);
        db.AddRange(author, editor, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(author);
        article.ContentMarkdown = $"Body\n\n![Image]({media.Url})";
        article.ContentHtml = $"<p>Body</p><p><img src=\"{media.Url}\" alt=\"Image\"></p>";
        article.MediaResources.Add(new ArticleMediaResource { MediaResourceId = media.Id });
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.UpdateAsync(
            article.Id,
            editor.Id,
            new UpdateArticleRequest
            {
                Title = "Updated",
                ContentMarkdown = article.ContentMarkdown,
                BodyMediaResourceIds = [media.Id],
                ConcurrencyStamp = article.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        response.LastEditor.Id.Should().Be(editor.Id);
        response.BodyMedia.Should().ContainSingle().Which.Id.Should().Be(media.Id);
    }

    /// <summary>
    /// Verifies pending resources cannot be attached to an article.
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldRejectPendingMedia()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var media = CreateMedia(editor, ResourceStatus.Pending);
        db.AddRange(editor, media);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateDraftAsync(
            editor.Id,
            CreateRequestWithMedia(media),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleMediaNotConfirmed.GetMessage());
    }

    /// <summary>
    /// Verifies Markdown image URLs and declared body resource identifiers must match exactly.
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldRejectUndeclaredOrAmbiguousBodyImages()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var first = CreateMedia(editor, ResourceStatus.Active);
        var second = CreateMedia(editor, ResourceStatus.Active);
        second.Url = first.Url;
        db.AddRange(editor, first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var undeclared = CreateRequestWithMedia(first) with { BodyMediaResourceIds = [] };
        var ambiguous = CreateRequestWithMedia(first) with
        {
            BodyMediaResourceIds = [first.Id, second.Id]
        };

        var undeclaredAction = () => service.CreateDraftAsync(
            editor.Id, undeclared, TestContext.Current.CancellationToken);
        var ambiguousAction = () => service.CreateDraftAsync(
            editor.Id, ambiguous, TestContext.Current.CancellationToken);

        await undeclaredAction.Should().ThrowAsync<ConflictException>();
        await ambiguousAction.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// Verifies publish re-renders canonical HTML and publish/unpublish advance status and concurrency.
    /// </summary>
    [Fact]
    public async Task PublishAndUnpublishShouldReRenderAndApplyStatusMachine()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor);
        article.ContentMarkdown = "# Canonical";
        article.ContentHtml = "<script>stale()</script>";
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var originalStamp = article.ConcurrencyStamp;

        var published = await service.PublishAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);

        published.Status.Should().Be(ArticleStatus.Published);
        published.ContentHtml.Should().Contain("<h1>Canonical</h1>").And.NotContain("script");
        published.ConcurrencyStamp.Should().NotBe(originalStamp);
        unpublished.Status.Should().Be(ArticleStatus.Draft);
        unpublished.PublishedAt.Should().BeNull();
        unpublished.ConcurrencyStamp.Should().NotBe(published.ConcurrencyStamp);
    }

    /// <summary>
    /// Verifies publication rechecks category activity instead of trusting associations saved on the draft.
    /// </summary>
    [Fact]
    public async Task PublishShouldRejectInactiveCategory()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        var category = new ArticleCategory
        {
            Name = "Inactive",
            Slug = "inactive",
            IsActive = false
        };
        var article = CreateArticle(editor);
        article.CategoryAssignments.Add(new ArticleCategoryAssignment
        {
            ArticleCategory = category,
            ArticleCategoryId = category.Id
        });
        db.AddRange(editor, category, article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.PublishAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.ArticleCategoryInactive.GetMessage());
        article.Status.Should().Be(ArticleStatus.Draft);
    }

    /// <summary>
    /// Verifies Draft can be archived with a new concurrency token and Archived rejects every write command.
    /// </summary>
    [Fact]
    public async Task ArchiveShouldMakeDraftTerminal()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var article = CreateArticle(editor);
        db.Articles.Add(article);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var originalStamp = article.ConcurrencyStamp;

        await service.ArchiveAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);

        article.Status.Should().Be(ArticleStatus.Archived);
        article.ConcurrencyStamp.Should().NotBe(originalStamp);
        var request = new UpdateArticleRequest
        {
            Title = "Changed",
            ContentMarkdown = "Changed",
            ConcurrencyStamp = article.ConcurrencyStamp
        };
        var update = () => service.UpdateAsync(
            article.Id, editor.Id, request, TestContext.Current.CancellationToken);
        var publish = () => service.PublishAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);
        var unpublish = () => service.UnpublishAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);
        var archive = () => service.ArchiveAsync(
            article.Id, editor.Id, TestContext.Current.CancellationToken);

        await update.Should().ThrowAsync<ConflictException>();
        await publish.Should().ThrowAsync<ConflictException>();
        await unpublish.Should().ThrowAsync<ConflictException>();
        await archive.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// Verifies Published articles cannot be directly updated or archived and Archived remains terminal.
    /// </summary>
    [Fact]
    public async Task PublishedAndArchivedArticlesShouldEnforceWriteBoundaries()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var published = CreateArticle(editor, status: ArticleStatus.Published);
        var archived = CreateArticle(editor, status: ArticleStatus.Archived);
        db.Articles.AddRange(published, archived);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var request = new UpdateArticleRequest
        {
            Title = "Changed",
            ContentMarkdown = "Changed",
            ConcurrencyStamp = published.ConcurrencyStamp
        };

        var updatePublished = () => service.UpdateAsync(
            published.Id, editor.Id, request, TestContext.Current.CancellationToken);
        var archivePublished = () => service.ArchiveAsync(
            published.Id, editor.Id, TestContext.Current.CancellationToken);
        var archiveArchived = () => service.ArchiveAsync(
            archived.Id, editor.Id, TestContext.Current.CancellationToken);

        await updatePublished.Should().ThrowAsync<ConflictException>();
        await archivePublished.Should().ThrowAsync<ConflictException>();
        await archiveArchived.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// Verifies public queries expose only Published articles through the public response contract.
    /// </summary>
    [Fact]
    public async Task PublicQueriesShouldOnlyReturnPublishedArticles()
    {
        await using var db = CreateDbContext();
        var editor = CreateUser("editor@example.com");
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var published = CreateArticle(editor, status: ArticleStatus.Published);
        var draft = CreateArticle(editor);
        db.Articles.AddRange(published, draft);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var page = await service.GetPublicListAsync(
            new ArticleListRequest(),
            TestContext.Current.CancellationToken);
        var detail = await service.GetPublicByIdAsync(
            published.Id,
            TestContext.Current.CancellationToken);
        var hidden = () => service.GetPublicByIdAsync(
            draft.Id,
            TestContext.Current.CancellationToken);

        page.Items.Should().ContainSingle().Which.Id.Should().Be(published.Id);
        detail.ContentHtml.Should().Be(published.ContentHtml);
        await hidden.Should().ThrowAsync<NotFoundException>();
    }

    private static ArticleService CreateService(ApplicationDbContext db)
        => new(
            db,
            new ArticleMarkdownRenderer(new HtmlContentSanitizer()),
            NullLogger<ArticleService>.Instance);

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
            ContentMarkdown = $"Body\n\n![Image]({media.Url})",
            BodyMediaResourceIds = [media.Id]
        };

    private static Article CreateArticle(
        User editor,
        ArticleCategory? category = null,
        ArticleStatus status = ArticleStatus.Draft)
    {
        var article = new Article
        {
            Title = $"Article {Guid.NewGuid():N}",
            ContentMarkdown = "Body",
            ContentHtml = "<p>Body</p>",
            Author = editor,
            AuthorId = editor.Id,
            LastEditor = editor,
            LastEditorId = editor.Id,
            Status = status,
            PublishedAt = status == ArticleStatus.Published ? DateTimeOffset.UtcNow : null,
            PublishedById = status == ArticleStatus.Published ? editor.Id : null
        };
        if (category is not null)
        {
            article.CategoryAssignments.Add(new ArticleCategoryAssignment
            {
                ArticleCategory = category,
                ArticleCategoryId = category.Id
            });
        }

        return article;
    }
}
