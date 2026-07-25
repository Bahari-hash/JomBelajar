using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        var editor = new User
        {
            Username = "editor@example.com",
            Email = "editor@example.com",
            PasswordHash = "hash",
            Role = UserRole.Editor
        };
        db.Users.Add(editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.PresignPutObjectAsync(
                It.IsAny<string>(), "image/png", 1024, It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://storage.example.com/presigned");
        storage.Setup(x => x.GetObjectMetadataAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(1024, "image/png"));
        storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(objectName => $"https://cdn.example.com/{objectName}");

        var mediaService = new MediaResourceService(
            db,
            storage.Object,
            new MediaUploadPolicy(Options.Create(TestUploadSettings.Create())),
            Options.Create(TestMultipartUploadSettings.Create()),
            TimeProvider.System);
        var presign = await mediaService.CreatePendingResourceAndPresignAsync(
            editor.Id,
            "lesson.png",
            ".png",
            1024,
            "image/png",
            ResourceModule.ArticlePicture,
            TestContext.Current.CancellationToken);

        var confirmed = await mediaService.ConfirmAsync(
            presign.ResourceId,
            editor.Id,
            TestContext.Current.CancellationToken);
        var articleService = new ArticleService(db, new HtmlContentSanitizer());

        var article = await articleService.CreateDraftAsync(
            editor.Id,
            new CreateArticleRequest
            {
                Title = "Lesson",
                ContentHtml = $"<p>Read this lesson.</p><img src=\"{confirmed.Url}\" alt=\"Lesson\">",
                MediaResourceIds = [confirmed.Id]
            },
            TestContext.Current.CancellationToken);

        confirmed.Status.Should().Be(ResourceStatus.Active);
        confirmed.Url.Should().StartWith("https://cdn.example.com/");
        article.MediaResourceIds.Should().ContainSingle().Which.Should().Be(confirmed.Id);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
