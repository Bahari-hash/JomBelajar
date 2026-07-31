using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频分类规范化、可见性计数和删除级联语义。
/// </summary>
public sealed class VideoCategoryServiceTests
{
    [Fact]
    public async Task CreateShouldNormalizeVideoCategoryValues()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var response = await service.CreateAsync(
            new CreateVideoCategoryRequest
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
    public async Task PublicCountShouldIncludeOnlyReadyPublishedVideos()
    {
        await using var db = CreateDbContext();
        var category = new VideoCategory { Name = "Grammar", Slug = "grammar" };
        var published = CreateVideo(VideoProcessingStatus.Ready, VideoPublicationStatus.Published);
        var draft = CreateVideo(VideoProcessingStatus.Ready, VideoPublicationStatus.Draft);
        published.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = published,
            VideoCategory = category,
            VideoId = published.Id,
            VideoCategoryId = category.Id
        });
        draft.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = draft,
            VideoCategory = category,
            VideoId = draft.Id,
            VideoCategoryId = category.Id
        });
        db.AddRange(category, published, draft);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetPublicListAsync(
            new VideoCategoryListRequest(),
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.VideoCount.Should().Be(1);
    }

    [Fact]
    public async Task AdminListShouldIncludeInactiveAndExcludeArchivedFromCount()
    {
        await using var db = CreateDbContext();
        var createdAt = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);
        var category = new VideoCategory
        {
            Name = "Legacy",
            Slug = "legacy",
            IsActive = false,
            CreatedAt = createdAt
        };
        var video = CreateVideo(VideoProcessingStatus.Queued, VideoPublicationStatus.Draft);
        var archived = CreateVideo(
            VideoProcessingStatus.Ready,
            VideoPublicationStatus.Archived);
        video.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = video,
            VideoCategory = category,
            VideoId = video.Id,
            VideoCategoryId = category.Id
        });
        archived.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = archived,
            VideoCategory = category,
            VideoId = archived.Id,
            VideoCategoryId = category.Id
        });
        db.AddRange(category, video, archived);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetAdminListAsync(
            new AdminVideoCategoryListRequest { IncludeInactive = true },
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.VideoCount.Should().Be(1);
        response.Items[0].IsActive.Should().BeFalse();
        response.Items[0].CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task DeleteShouldRejectCategoryInUseWithoutClearingAssignments()
    {
        await using var db = CreateDbContext();
        var category = new VideoCategory { Name = "Grammar", Slug = "grammar" };
        var video = CreateVideo(VideoProcessingStatus.Ready, VideoPublicationStatus.Published);
        video.CategoryAssignments.Add(new VideoCategoryAssignment
        {
            Video = video,
            VideoCategory = category,
            VideoId = video.Id,
            VideoCategoryId = category.Id
        });
        db.AddRange(category, video);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.DeleteAsync(
            category.Id,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.VideoCategoryInUse);
        (await db.VideoCategories.AnyAsync(
            value => value.Id == category.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await db.VideoCategoryAssignments.AnyAsync(
            value => value.VideoId == video.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await db.Videos.AnyAsync(
            value => value.Id == video.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task ClearShouldRejectMissingCategoryBeforeBulkDelete()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var action = () => service.ClearVideosAsync(
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    private static VideoCategoryService CreateService(ApplicationDbContext db)
        => new(db, new Mock<IDatabaseExceptionClassifier>().Object);

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Video CreateVideo(
        VideoProcessingStatus processingStatus,
        VideoPublicationStatus publicationStatus)
        => new()
        {
            CreatedById = Guid.NewGuid(),
            SourceMediaResourceId = Guid.NewGuid(),
            Title = "Video",
            OriginalLanguage = "en",
            ProcessingStatus = processingStatus,
            PublicationStatus = publicationStatus
        };
}
