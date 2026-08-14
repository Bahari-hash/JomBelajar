using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Policies;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class ArticleMediaFlowTests
{
    [Fact]
    public async Task ConfirmedArticlePictureShouldBeAcceptedByArticleService()
    {
        await using var db = CreateDbContext();
        var admin = new User
        {
            Email = "admin@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.PresignPutObjectAsync(
                It.IsAny<string>(), "image/png", 1024, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.example.com/presigned");
        storage.Setup(x => x.GetObjectMetadataAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(1024, "image/png"));
        storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(objectName => $"https://oss.example.com/{objectName}");

        var mediaService = new MediaResourceService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaResourceService>.Instance,
            db,
            storage.Object,
            new MediaUploadPolicy(Options.Create(TestUploadSettings.Create())),
            Options.Create(TestMultipartUploadSettings.Create()),
            TimeProvider.System);
        var presign = await mediaService.CreatePendingResourceAndPresignAsync(
            admin.Id,
            "lesson.png",
            ".png",
            1024,
            "image/png",
            ResourceModule.ArticlePicture,
            TestContext.Current.CancellationToken);

        var confirmed = await mediaService.ConfirmAsync(
            presign.ResourceId,
            admin.Id,
            TestContext.Current.CancellationToken);
        var articleService = new ArticleService(
            db,
            new ArticleMarkdownRenderer(new HtmlContentSanitizer()),
            NullLogger<ArticleService>.Instance);

        var article = await articleService.CreateDraftAsync(
            admin.Id,
            new CreateArticleRequest
            {
                Title = "Lesson",
                ContentMarkdown = $"Read this lesson.\n\n![Lesson]({confirmed.Url})",
                BodyMediaResourceIds = [confirmed.Id]
            },
            TestContext.Current.CancellationToken);

        confirmed.Status.Should().Be(ResourceStatus.Active);
        confirmed.Url.Should().StartWith("https://oss.example.com/");
        article.BodyMedia.Should().ContainSingle().Which.Id.Should().Be(confirmed.Id);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
