using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
/// 验证视频源绑定、发布可见性和进度完成判定。
/// </summary>
public sealed class VideoServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveCourseVideoShouldCreateVideoAndJobAcrossAdmins()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var source = CreateResource(Guid.NewGuid(), ResourceModule.CourseVideo, ResourceStatus.Active);
        db.MediaResources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateAsync(adminId, new CreateVideoRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "  Listening lesson  ",
            Description = " practice ",
        }, TestContext.Current.CancellationToken);

        response.Title.Should().Be("Listening lesson");
        response.ProcessingStatus.Should().Be(VideoProcessingStatus.Queued);
        (await db.VideoProcessingJobs.SingleAsync(
            TestContext.Current.CancellationToken)).OutputVersion.Should().NotBeEmpty();
        var video = await db.Videos.SingleAsync(TestContext.Current.CancellationToken);
        video.CreatedById.Should().Be(adminId);
        video.LastEditorId.Should().Be(adminId);
    }

    [Fact]
    public async Task ActiveVideoCoverShouldBeAssociatedAcrossAdmins()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var source = CreateResource(adminId, ResourceModule.CourseVideo, ResourceStatus.Active);
        var cover = CreateResource(Guid.NewGuid(), ResourceModule.VideoCover, ResourceStatus.Active);
        cover.OriginalName = "cover.webp";
        cover.Url = "https://media.example.test/covers/cover.webp";
        db.AddRange(source, cover);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateAsync(adminId, new CreateVideoRequest
        {
            SourceMediaResourceId = source.Id,
            CoverMediaResourceId = cover.Id,
            Title = "Covered lesson",
        }, TestContext.Current.CancellationToken);

        response.Cover.Should().Be(new VideoCoverSummaryResponse(
            cover.Id,
            "cover.webp",
            "https://media.example.test/covers/cover.webp"));
        (await db.Videos.SingleAsync(TestContext.Current.CancellationToken))
            .CoverMediaResourceId.Should().Be(cover.Id);
    }

    [Theory]
    [InlineData(ResourceModule.ArticlePicture, ResourceStatus.Active,
        "https://media.example.test/cover.jpg", ErrorCodes.VideoCoverModuleInvalid)]
    [InlineData(ResourceModule.VideoCover, ResourceStatus.Pending,
        "https://media.example.test/cover.jpg", ErrorCodes.VideoCoverNotActive)]
    [InlineData(ResourceModule.VideoCover, ResourceStatus.Active,
        "file:///private/cover.jpg", ErrorCodes.VideoCoverUrlInvalid)]
    public async Task CreateShouldRejectInvalidCoverResourceContract(
        ResourceModule module,
        ResourceStatus status,
        string url,
        ErrorCodes expectedCode)
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var source = CreateResource(adminId, ResourceModule.CourseVideo, ResourceStatus.Active);
        var cover = CreateResource(Guid.NewGuid(), module, status);
        cover.Url = url;
        db.AddRange(source, cover);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateAsync(adminId, new CreateVideoRequest
        {
            SourceMediaResourceId = source.Id,
            CoverMediaResourceId = cover.Id,
            Title = "Lesson",
        }, TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(expectedCode);
    }

    [Fact]
    public async Task UpdateCoverShouldKeepSetAndClearWithoutDeletingResources()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var first = CreateResource(adminId, ResourceModule.VideoCover, ResourceStatus.Active);
        first.Url = "https://media.example.test/covers/first.jpg";
        var second = CreateResource(Guid.NewGuid(), ResourceModule.VideoCover, ResourceStatus.Active);
        second.Url = "https://media.example.test/covers/second.jpg";
        var video = CreateVideo(adminId);
        video.CoverMediaResourceId = first.Id;
        video.CoverMediaResource = first;
        db.AddRange(first, second, video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var kept = await service.UpdateAsync(video.Id, adminId, new UpdateVideoRequest
        {
            Title = "Kept",
            CoverAction = VideoCoverAction.Keep,
            ConcurrencyStamp = video.ConcurrencyStamp
        }, TestContext.Current.CancellationToken);
        var replaced = await service.UpdateAsync(video.Id, adminId, new UpdateVideoRequest
        {
            Title = "Replaced",
            CoverAction = VideoCoverAction.Set,
            CoverMediaResourceId = second.Id,
            ConcurrencyStamp = kept.ConcurrencyStamp
        }, TestContext.Current.CancellationToken);
        var cleared = await service.UpdateAsync(video.Id, adminId, new UpdateVideoRequest
        {
            Title = "Cleared",
            CoverAction = VideoCoverAction.Clear,
            ConcurrencyStamp = replaced.ConcurrencyStamp
        }, TestContext.Current.CancellationToken);

        kept.Cover!.Id.Should().Be(first.Id);
        replaced.Cover!.Id.Should().Be(second.Id);
        cleared.Cover.Should().BeNull();
        (await db.MediaResources.CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task CreateShouldAssociateAllEnabledVideoCategories()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var source = CreateResource(Guid.NewGuid(), ResourceModule.CourseVideo, ResourceStatus.Active);
        var first = new VideoCategory { Name = "Grammar", Slug = "grammar" };
        var second = new VideoCategory { Name = "Listening", Slug = "listening" };
        db.AddRange(source, first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateAsync(adminId, new CreateVideoRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "Lesson",
            CategoryIds = [second.Id, first.Id]
        }, TestContext.Current.CancellationToken);

        response.Categories.Select(value => value.Id)
            .Should().BeEquivalentTo([first.Id, second.Id]);
        (await db.VideoCategoryAssignments.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task UpdateShouldSynchronizeVideoCategoryTargetSet()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var first = new VideoCategory { Name = "Grammar", Slug = "grammar" };
        var second = new VideoCategory { Name = "Listening", Slug = "listening" };
        var video = CreateVideo(ownerId);
        video.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = video,
            VideoCategory = first,
            VideoId = video.Id,
            VideoCategoryId = first.Id
        });
        db.AddRange(first, second, video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.UpdateAsync(video.Id, ownerId, new UpdateVideoRequest
        {
            Title = "Updated",
            CategoryIds = [second.Id],
            ConcurrencyStamp = video.ConcurrencyStamp
        }, TestContext.Current.CancellationToken);

        var assignment = await db.VideoCategoryAssignments.SingleAsync(
            TestContext.Current.CancellationToken);
        assignment.VideoCategoryId.Should().Be(second.Id);
    }

    [Fact]
    public async Task AnotherAdminShouldUpdateGlobalVideoAndBecomeLastEditor()
    {
        await using var db = CreateDbContext();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var video = CreateVideo(creatorId);
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.UpdateAsync(video.Id, adminId, new UpdateVideoRequest
        {
            Title = "Updated",
            ConcurrencyStamp = video.ConcurrencyStamp
        }, TestContext.Current.CancellationToken);

        video.CreatedById.Should().Be(creatorId);
        video.LastEditorId.Should().Be(adminId);
        video.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task CreateShouldRejectInactiveVideoCategoryWithoutSavingVideo()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var source = CreateResource(ownerId, ResourceModule.CourseVideo, ResourceStatus.Active);
        var category = new VideoCategory
        {
            Name = "Legacy",
            Slug = "legacy",
            IsActive = false
        };
        db.AddRange(source, category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateAsync(ownerId, new CreateVideoRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "Lesson",
            CategoryIds = [category.Id]
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
        (await db.Videos.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task CatalogShouldHideInactiveCategoriesAndFilterOnlyActiveCategories()
    {
        await using var db = CreateDbContext();
        var creator = CreateUser("creator", "Video Creator", "https://media.example.test/avatar.jpg");
        db.Users.Add(creator);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var active = new VideoCategory { Name = "Grammar", Slug = "grammar" };
        var inactive = new VideoCategory
        {
            Name = "Legacy",
            Slug = "legacy",
            IsActive = false
        };
        var video = CreateVideo(creator.Id);
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.PublicationStatus = VideoPublicationStatus.Published;
        video.DurationSeconds = 120;
        video.DisplayWidth = 1920;
        video.DisplayHeight = 1080;
        video.PublishedAt = Now;
        video.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = video,
            VideoCategory = active,
            VideoId = video.Id,
            VideoCategoryId = active.Id
        });
        video.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = video,
            VideoCategory = inactive,
            VideoId = video.Id,
            VideoCategoryId = inactive.Id
        });
        db.AddRange(active, inactive, video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var catalog = await service.GetCatalogAsync(
            new VideoCatalogRequest(),
            TestContext.Current.CancellationToken);
        var inactiveFilter = await service.GetCatalogAsync(
            new VideoCatalogRequest { CategoryId = inactive.Id },
            TestContext.Current.CancellationToken);

        var item = catalog.Items.Should().ContainSingle().Which;
        item.Categories.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        item.Author.Should().Be(new VideoUserSummaryResponse(
            creator.Id,
            "Video Creator",
            "https://media.example.test/avatar.jpg"));
        inactiveFilter.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CatalogAndDetailsShouldUseGeneratedPosterWithoutCustomCover()
    {
        await using var db = CreateDbContext();
        var creator = CreateUser("poster-owner");
        db.Users.Add(creator);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var video = CreateVideo(creator.Id);
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.PublicationStatus = VideoPublicationStatus.Published;
        video.DurationSeconds = 120;
        video.DisplayWidth = 1920;
        video.DisplayHeight = 1080;
        video.PublishedAt = Now;
        video.PosterObjectName = "videos/id/outputs/version/poster.jpg";
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var delivery = new Mock<IVideoDeliveryUrlService>();
        delivery.Setup(value => value.CreateUrl(
                video.PosterObjectName,
                "videos/id/outputs/version/"))
            .Returns(new VideoDeliveryUrl(
                "https://media.example.test/videos/id/outputs/version/poster.jpg",
                null));
        var service = CreateService(db, deliveryUrlService: delivery.Object);

        var catalog = await service.GetCatalogAsync(
            new VideoCatalogRequest(),
            TestContext.Current.CancellationToken);
        var details = await service.GetDetailsAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        const string expectedPosterUrl =
            "https://media.example.test/videos/id/outputs/version/poster.jpg";
        catalog.Items.Should().ContainSingle().Which.CoverUrl.Should().Be(expectedPosterUrl);
        details.CoverUrl.Should().Be(expectedPosterUrl);
        delivery.Verify(value => value.CreateUrl(
            video.PosterObjectName,
            "videos/id/outputs/version/"), Times.Exactly(2));
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

    [Theory]
    [InlineData(VideoPublicationStatus.Draft)]
    [InlineData(VideoPublicationStatus.Published)]
    [InlineData(VideoPublicationStatus.Unpublished)]
    [InlineData(VideoPublicationStatus.Archived)]
    public async Task AdminPlaybackShouldAllowReadyVideoRegardlessOfPublicationStatus(
        VideoPublicationStatus publicationStatus)
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.PublicationStatus = publicationStatus;
        video.CurrentOutputVersion = Guid.NewGuid();
        video.DurationSeconds = 90;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var delivery = new Mock<IVideoDeliveryUrlService>();
        delivery.Setup(value => value.CreateUrl(
                "videos/id/outputs/version/master.m3u8",
                "videos/id/outputs/version/"))
            .Returns(new VideoDeliveryUrl(
                "https://media.example.test/master.m3u8",
                Now.AddMinutes(5)));
        var progress = new Mock<IUserVideoProgressStore>();
        var service = CreateService(db, progress.Object, delivery.Object);

        var response = await service.GetAdminPlaybackAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        response.MasterPlaylistUrl.Should().Be(
            "https://media.example.test/master.m3u8");
        response.PositionSeconds.Should().Be(0);
        response.IsCompleted.Should().BeFalse();
        progress.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AdminPlaybackShouldPreferCustomCoverOverGeneratedPoster()
    {
        await using var db = CreateDbContext();
        var cover = CreateResource(Guid.NewGuid(), ResourceModule.VideoCover, ResourceStatus.Active);
        cover.Url = "https://media.example.test/covers/custom.jpg";
        var video = CreateVideo(Guid.NewGuid());
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.CurrentOutputVersion = Guid.NewGuid();
        video.DurationSeconds = 90;
        video.PosterObjectName = "videos/id/outputs/version/poster.jpg";
        video.CoverMediaResourceId = cover.Id;
        video.CoverMediaResource = cover;
        db.AddRange(cover, video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var delivery = new Mock<IVideoDeliveryUrlService>();
        delivery.Setup(value => value.CreateUrl(
                video.MasterPlaylistObjectName!,
                "videos/id/outputs/version/"))
            .Returns(new VideoDeliveryUrl("https://media.example.test/master.m3u8", null));
        var service = CreateService(db, deliveryUrlService: delivery.Object);

        var response = await service.GetAdminPlaybackAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        response.PosterUrl.Should().Be("https://media.example.test/covers/custom.jpg");
        delivery.Verify(value => value.CreateUrl(
            video.PosterObjectName!,
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AdminPlaybackShouldUseGeneratedPosterWithoutCustomCover()
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.CurrentOutputVersion = Guid.NewGuid();
        video.DurationSeconds = 90;
        video.PosterObjectName = "videos/id/outputs/version/poster.jpg";
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var delivery = new Mock<IVideoDeliveryUrlService>();
        delivery.Setup(value => value.CreateUrl(
                video.MasterPlaylistObjectName!,
                "videos/id/outputs/version/"))
            .Returns(new VideoDeliveryUrl("https://media.example.test/master.m3u8", null));
        delivery.Setup(value => value.CreateUrl(
                video.PosterObjectName,
                "videos/id/outputs/version/"))
            .Returns(new VideoDeliveryUrl("https://media.example.test/poster.jpg", null));
        var service = CreateService(db, deliveryUrlService: delivery.Object);

        var response = await service.GetAdminPlaybackAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        response.PosterUrl.Should().Be("https://media.example.test/poster.jpg");
    }

    [Theory]
    [InlineData(VideoProcessingStatus.Queued, true)]
    [InlineData(VideoProcessingStatus.Processing, true)]
    [InlineData(VideoProcessingStatus.Failed, true)]
    [InlineData(VideoProcessingStatus.Ready, false)]
    public async Task AdminPlaybackShouldRejectUnreadyOrIncompleteVideo(
        VideoProcessingStatus processingStatus,
        bool completeOutput)
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.ProcessingStatus = processingStatus;
        video.DurationSeconds = 90;
        video.CurrentOutputVersion = completeOutput ? Guid.NewGuid() : null;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.GetAdminPlaybackAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UserPlaybackShouldRejectReadyDraftVideo()
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.DurationSeconds = 90;
        video.DisplayWidth = 1280;
        video.DisplayHeight = 720;
        video.CurrentOutputVersion = Guid.NewGuid();
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.GetPlaybackAsync(
            video.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ReadyVideoPublishShouldBeIdempotentAndVisible()
    {
        await using var db = CreateDbContext();
        var creator = CreateUser("publisher", "Video Publisher", "https://media.example.test/publisher.jpg");
        db.Users.Add(creator);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var ownerId = creator.Id;
        var video = CreateVideo(ownerId);
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.DurationSeconds = 100;
        video.DisplayWidth = 1280;
        video.DisplayHeight = 720;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var published = await service.PublishAsync(
            video.Id,
            ownerId,
            new VideoMutationRequest { ConcurrencyStamp = video.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var repeated = await service.PublishAsync(
            video.Id,
            ownerId,
            new VideoMutationRequest { ConcurrencyStamp = published.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var details = await service.GetDetailsAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        repeated.PublicationStatus.Should().Be(VideoPublicationStatus.Published);
        details.Id.Should().Be(video.Id);
        details.PublishedAt.Should().Be(Now);
        details.Author.Should().Be(new VideoUserSummaryResponse(
            creator.Id,
            "Video Publisher",
            "https://media.example.test/publisher.jpg"));
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
    public async Task StaleStampShouldConflictBeforeVideoStateValidation()
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.PublicationStatus = VideoPublicationStatus.Published;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.UpdateAsync(video.Id, Guid.NewGuid(), new UpdateVideoRequest
        {
            Title = "Updated",
            ConcurrencyStamp = Guid.NewGuid()
        }, TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.VideoConcurrencyConflict);
    }

    [Fact]
    public async Task PublishedVideoShouldRequireUnpublishBeforeMetadataUpdate()
    {
        await using var db = CreateDbContext();
        var video = CreateVideo(Guid.NewGuid());
        video.PublicationStatus = VideoPublicationStatus.Published;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.UpdateAsync(video.Id, Guid.NewGuid(), new UpdateVideoRequest
        {
            Title = "Updated",
            ConcurrencyStamp = video.ConcurrencyStamp
        }, TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.VideoStatusConflict);
    }

    [Fact]
    public async Task ArchiveShouldBeIdempotentAndHideVideoFromUserQueries()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var video = CreateVideo(adminId);
        video.ProcessingStatus = VideoProcessingStatus.Ready;
        video.DurationSeconds = 100;
        video.DisplayWidth = 1280;
        video.DisplayHeight = 720;
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var archived = await service.ArchiveAsync(
            video.Id,
            adminId,
            new VideoMutationRequest { ConcurrencyStamp = video.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var repeated = await service.ArchiveAsync(
            video.Id,
            adminId,
            new VideoMutationRequest { ConcurrencyStamp = archived.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var details = () => service.GetDetailsAsync(
            video.Id,
            TestContext.Current.CancellationToken);

        archived.PublicationStatus.Should().Be(VideoPublicationStatus.Archived);
        archived.ArchivedAt.Should().Be(Now);
        repeated.ConcurrencyStamp.Should().Be(archived.ConcurrencyStamp);
        await details.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ArchiveShouldRejectVideoWithActiveProcessingJob()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var video = CreateVideo(adminId);
        video.ProcessingJobs.Add(new VideoProcessingJob
        {
            VideoId = video.Id,
            OutputVersion = Guid.NewGuid(),
            Status = VideoProcessingJobStatus.Processing
        });
        db.Videos.Add(video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.ArchiveAsync(
            video.Id,
            adminId,
            new VideoMutationRequest { ConcurrencyStamp = video.ConcurrencyStamp },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.VideoArchiveConflict);
    }

    [Fact]
    public async Task AdminListShouldExcludeArchivedByDefaultAndReturnSafeAuditAndJobSummary()
    {
        await using var db = CreateDbContext();
        var creator = new User
        {
            Email = "creator@example.com",
            PasswordHash = "hash",
            Nickname = "Creator"
        };
        var editor = new User
        {
            Email = "editor@example.com",
            PasswordHash = "hash",
            Nickname = "Editor"
        };
        db.Users.AddRange(creator, editor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var visible = CreateVideo(creator.Id);
        visible.LastEditorId = editor.Id;
        visible.ProcessingJobs.Add(new VideoProcessingJob
        {
            VideoId = visible.Id,
            OutputVersion = Guid.NewGuid(),
            Status = VideoProcessingJobStatus.Failed,
            AttemptCount = 2,
            FailureCode = "ProbeFailed"
        });
        var archived = CreateVideo(creator.Id);
        archived.PublicationStatus = VideoPublicationStatus.Archived;
        archived.ArchivedAt = Now;
        db.Videos.AddRange(visible, archived);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var defaultPage = await service.GetAdminListAsync(
            new AdminVideoListRequest { CreatedById = creator.Id },
            TestContext.Current.CancellationToken);
        var archivedPage = await service.GetAdminListAsync(
            new AdminVideoListRequest
            {
                PublicationStatus = VideoPublicationStatus.Archived,
                CreatedById = creator.Id
            },
            TestContext.Current.CancellationToken);

        var item = defaultPage.Items.Should().ContainSingle().Which;
        item.Id.Should().Be(visible.Id);
        item.CreatedBy.Nickname.Should().Be("Creator");
        item.LastEditor.Nickname.Should().Be("Editor");
        item.LatestJob.Should().NotBeNull();
        item.LatestJob!.FailureCode.Should().Be("ProbeFailed");
        typeof(VideoProcessingJobSummaryResponse).GetProperty("LeaseOwner").Should().BeNull();
        archivedPage.Items.Should().ContainSingle().Which.Id.Should().Be(archived.Id);
    }

    private static VideoService CreateService(
        ApplicationDbContext db,
        IUserVideoProgressStore? progressStore = null,
        IVideoDeliveryUrlService? deliveryUrlService = null)
        => new(
            db,
            deliveryUrlService ?? Mock.Of<IVideoDeliveryUrlService>(),
            progressStore ?? Mock.Of<IUserVideoProgressStore>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            Options.Create(new VideoProgressSettings()),
            new TestTimeProvider(Now));

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Video CreateVideo(Guid ownerId)
        => new()
        {
            CreatedById = ownerId,
            LastEditorId = ownerId,
            SourceMediaResourceId = Guid.NewGuid(),
            Title = "Video",
            MasterPlaylistObjectName = "videos/id/outputs/version/master.m3u8"
        };

    private static User CreateUser(
        string username,
        string? nickname = null,
        string? avatarUrl = null)
        => new()
        {
            Email = $"{username}@example.test",
            PasswordHash = "hash",
            Nickname = nickname,
            AvatarUrl = avatarUrl
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
