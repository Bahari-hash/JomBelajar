using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频源绑定、发布可见性、字幕基础校验和进度完成判定。
/// </summary>
public sealed class VideoServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveOwnedCourseVideoShouldCreateVideoAndJobTogether()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var source = CreateResource(ownerId, ResourceModule.CourseVideo, ResourceStatus.Active);
        db.MediaResources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateAsync(ownerId, new CreateVideoRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "  Listening lesson  ",
            Description = " practice ",
            OriginalLanguage = "EN-US"
        }, TestContext.Current.CancellationToken);

        response.Title.Should().Be("Listening lesson");
        response.OriginalLanguage.Should().Be("en-us");
        response.ProcessingStatus.Should().Be(VideoProcessingStatus.Queued);
        (await db.VideoProcessingJobs.SingleAsync(
            TestContext.Current.CancellationToken)).OutputVersion.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DraftOrUnreadyVideoShouldBeHiddenFromUserDetails()
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.GetDetailsAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ReadyVideoPublishShouldBeIdempotentAndVisible()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var video = CreateVideo(ownerId);
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.DurationSeconds = 100;
        video.DisplayWidth = 1280;
        video.DisplayHeight = 720;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.PublishAsync(video.Id, ownerId, TestContext.Current.CancellationToken);
        var repeated = await service.PublishAsync(
            video.Id,
            ownerId,
            TestContext.Current.CancellationToken);
        var details = await service.GetDetailsAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        repeated.PublicationStatus.Should().Be(VideoPublicationStatus.Published);
        details.Id.Should().Be(video.Id);
        details.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public async Task ProgressShouldClampToleranceAndMarkCompletionOnServer()
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.PublicationStatus = VideoPublicationStatus.Published;
        video.DurationSeconds = 100;
        video.DisplayWidth = 1280;
        video.DisplayHeight = 720;
        video.PublishedAt = Now;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var progress = new Mock<IUserVideoProgressStore>();
        var service = CreateService(db, progressStore: progress.Object);
        var userId = Guid.NewGuid();

        await service.UpdateProgressAsync(
            video.Id,
            userId,
            new UpdateVideoProgressRequest { PositionSeconds = 102 },
            TestContext.Current.CancellationToken);

        progress.Verify(value => value.UpsertAsync(
            userId,
            video.Id,
            100,
            true,
            Now,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ActiveWebVttShouldBeValidatedAndAssociatedAsDefault()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var video = CreateVideo(ownerId);
        var subtitleResource = CreateResource(
            ownerId,
            ResourceModule.VideoSubtitle,
            ResourceStatus.Active);
        subtitleResource.ObjectName = "subtitles/example.vtt";
        subtitleResource.ContentType = "text/vtt";
        var bytes = Encoding.UTF8.GetBytes("WEBVTT\n\n00:00.000 --> 00:01.000\nHello\n");
        subtitleResource.Size = bytes.Length;
        db.AddRange(video, subtitleResource);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.GetObjectMetadataAsync(
                subtitleResource.ObjectName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(bytes.Length, "text/vtt"));
        storage.Setup(value => value.DownloadObjectAsync(
                subtitleResource.ObjectName,
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Stream destination, CancellationToken _) =>
            {
                destination.Write(bytes);
                return Task.CompletedTask;
            });
        var service = CreateService(db, storage: storage.Object);

        var result = await service.AddSubtitleAsync(
            video.Id,
            ownerId,
            new AddVideoSubtitleRequest
            {
                MediaResourceId = subtitleResource.Id,
                LanguageTag = "EN",
                DisplayName = "English",
                IsDefault = true
            },
            TestContext.Current.CancellationToken);

        result.LanguageTag.Should().Be("en");
        result.IsDefault.Should().BeTrue();
    }

    private static VideoService CreateService(
        ApplicationDbContext db,
        IObjectStorageService? storage = null,
        IUserVideoProgressStore? progressStore = null)
        => new(
            db,
            storage ?? Mock.Of<IObjectStorageService>(),
            Mock.Of<IVideoDeliveryUrlService>(),
            progressStore ?? Mock.Of<IUserVideoProgressStore>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            Options.Create(new VideoProgressSettings()),
            Options.Create(TestUploadSettings.Create()),
            new TestTimeProvider(Now),
            NullLogger<VideoService>.Instance);

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Video CreateVideo(Guid ownerId)
        => new()
        {
            OwnerId = ownerId,
            SourceMediaResourceId = Guid.NewGuid(),
            Title = "Video",
            OriginalLanguage = "en",
            MasterPlaylistObjectName = "videos/id/outputs/version/master.m3u8"
        };

    private static MediaResource CreateResource(
        Guid uploaderId,
        ResourceModule module,
        ResourceStatus status)
        => new()
        {
            UploaderId = uploaderId,
            ObjectName = $"objects/{Guid.NewGuid():N}",
            OriginalName = "media.bin",
            Module = module,
            Status = status,
            Size = 1024,
            Extension = ".bin",
            ContentType = "application/octet-stream"
        };
}
