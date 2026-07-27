using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频处理任务领取、租约和永久失败终态。
/// </summary>
public sealed class VideoProcessingServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DispatchAndClaimShouldSetLeaseAttemptAndVideoProcessingState()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedVideo(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var service = CreateService(db);

        var result = await service.GetDispatchableJobIdsAsync(
            TestContext.Current.CancellationToken);
        await service.MarkDispatchedAsync(
            job.Id,
            TestContext.Current.CancellationToken);
        var claimed = await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);
        var storedJob = await db.VideoProcessingJobs.AsNoTracking()
            .Include(value => value.Video)
            .SingleAsync(TestContext.Current.CancellationToken);

        result.Should().ContainSingle().Which.Should().Be(job.Id);
        claimed.Should().BeTrue();
        storedJob.LastDispatchedAt.Should().Be(Now);
        storedJob.Status.Should().Be(VideoProcessingJobStatus.Processing);
        storedJob.AttemptCount.Should().Be(1);
        storedJob.LeaseOwner.Should().Be(workerId);
        storedJob.Video.ProcessingStatus.Should().Be(VideoProcessingStatus.Processing);
    }

    [Fact]
    public async Task RecentlyDispatchedJobShouldWaitForThrottleWindow()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedVideo(db);
        job.LastDispatchedAt = Now.AddSeconds(-10);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var result = await service.GetDispatchableJobIdsAsync(
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpiredLeaseShouldBeDispatchableAndClaimableAgain()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedVideo(db);
        job.Status = VideoProcessingJobStatus.Processing;
        job.LeaseOwner = Guid.NewGuid();
        job.LeaseExpiresAt = Now.AddMinutes(-1);
        job.LastDispatchedAt = Now.AddMinutes(-1);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var newWorkerId = Guid.NewGuid();

        var dispatchable = await service.GetDispatchableJobIdsAsync(
            TestContext.Current.CancellationToken);
        var claimed = await service.TryClaimAsync(
            job.Id,
            newWorkerId,
            TestContext.Current.CancellationToken);

        dispatchable.Should().ContainSingle().Which.Should().Be(job.Id);
        claimed.Should().BeTrue();
        var storedJob = await db.VideoProcessingJobs.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedJob.LeaseOwner.Should().Be(newWorkerId);
        storedJob.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task DuplicateClaimShouldBeIgnoredWhileLeaseIsActive()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedVideo(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var first = await service.TryClaimAsync(
            job.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        var duplicate = await service.TryClaimAsync(
            job.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        first.Should().BeTrue();
        duplicate.Should().BeFalse();
    }

    [Fact]
    public async Task PermanentProbeFailureShouldNotAutomaticallyRetry()
    {
        await using var db = CreateDbContext();
        var (video, job) = AddQueuedVideo(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var probe = new Mock<IMediaProbe>();
        probe.Setup(value => value.ProbeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VideoProcessingException(
                VideoProcessingFailureCode.AudioStreamMissing,
                isTransient: false));
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.DownloadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(value => value.ListObjectNamesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = CreateService(db, storage.Object, probe.Object);
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        var storedJob = await db.VideoProcessingJobs.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        var storedVideo = await db.Videos.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedJob.Status.Should().Be(VideoProcessingJobStatus.Failed);
        storedJob.NextAttemptAt.Should().BeNull();
        storedVideo.ProcessingStatus.Should().Be(VideoProcessingStatus.Failed);
        storedVideo.LastFailureCode.Should().Be("AudioStreamMissing");
    }

    [Fact]
    public async Task SuccessfulProcessingShouldUploadMasterLastBeforeReady()
    {
        await using var db = CreateDbContext();
        var (video, job) = AddQueuedVideo(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var uploaded = new List<string>();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.DownloadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(value => value.UploadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                It.IsAny<ObjectStorageUploadOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Stream, long, ObjectStorageUploadOptions, CancellationToken>(
                (objectName, _, _, _, _) => uploaded.Add(objectName))
            .Returns(Task.CompletedTask);
        storage.Setup(value => value.GetObjectMetadataAsync(
                It.Is<string>(name => name.EndsWith("master.m3u8", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(
                128,
                "application/vnd.apple.mpegurl"));
        var probe = new Mock<IMediaProbe>();
        probe.Setup(value => value.ProbeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaProbeResult(60, 1280, 720, "mp4", "h264", "aac"));
        var service = CreateService(
            db,
            storage.Object,
            probe.Object,
            new FakeVideoTranscoder());
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        uploaded.Should().HaveCount(4);
        uploaded[0].Should().EndWith("480p/segment_000001.ts");
        uploaded[1].Should().EndWith("480p/index.m3u8");
        uploaded[2].Should().EndWith("poster.jpg");
        uploaded[3].Should().EndWith("master.m3u8");
        var storedVideo = await db.Videos.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedVideo.ProcessingStatus.Should().Be(VideoProcessingStatus.Ready);
        storedVideo.CurrentOutputVersion.Should().Be(job.OutputVersion);
        (await db.VideoRenditions.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken)).Height.Should().Be(480);
    }

    private static VideoProcessingService CreateService(
        ApplicationDbContext db,
        IObjectStorageService? storage = null,
        IMediaProbe? probe = null,
        IVideoTranscoder? transcoder = null)
        => new(
            db,
            storage ?? Mock.Of<IObjectStorageService>(),
            probe ?? Mock.Of<IMediaProbe>(),
            transcoder ?? Mock.Of<IVideoTranscoder>(),
            Options.Create(new VideoProcessingSettings
            {
                TemporaryDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "tiny-lang-video-tests"),
                MinimumFreeDiskMB = 1
            }),
            new TestTimeProvider(Now),
            NullLogger<VideoProcessingService>.Instance);

    private static (Video Video, VideoProcessingJob Job) AddQueuedVideo(
        ApplicationDbContext db)
    {
        var resource = new MediaResource
        {
            UploaderId = Guid.NewGuid(),
            ObjectName = "courses/source.mp4",
            OriginalName = "source.mp4",
            Module = ResourceModule.CourseVideo,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp4",
            ContentType = "video/mp4"
        };
        var video = new Video
        {
            OwnerId = resource.UploaderId,
            SourceMediaResourceId = resource.Id,
            SourceMediaResource = resource,
            Title = "Video",
            OriginalLanguage = "en"
        };
        var job = new VideoProcessingJob
        {
            VideoId = video.Id,
            Video = video,
            OutputVersion = Guid.NewGuid()
        };
        video.ProcessingJobs.Add(job);
        db.Add(video);
        return (video, job);
    }

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// 在 job 输出目录创建可验证上传顺序的最小 HLS 文件集合。
    /// </summary>
    private sealed class FakeVideoTranscoder : IVideoTranscoder
    {
        /// <inheritdoc />
        public Task<VideoTranscodeResult> TranscodeAsync(
            string inputPath,
            string outputDirectory,
            MediaProbeResult probeResult,
            CancellationToken cancellationToken = default)
        {
            var renditionDirectory = Path.Combine(outputDirectory, "480p");
            Directory.CreateDirectory(renditionDirectory);
            var segment = Path.Combine(renditionDirectory, "segment_000001.ts");
            var playlist = Path.Combine(renditionDirectory, "index.m3u8");
            var poster = Path.Combine(outputDirectory, "poster.jpg");
            var master = Path.Combine(outputDirectory, "master.m3u8");
            File.WriteAllText(segment, "segment");
            File.WriteAllText(playlist, "segment_000001.ts");
            File.WriteAllText(poster, "poster");
            File.WriteAllText(master, "480p/index.m3u8");
            var plan = new VideoRenditionPlan(
                "480p", 480, 854, 480, 1200, 128, "avc1.64001f,mp4a.40.2");
            return Task.FromResult(new VideoTranscodeResult(
                master,
                poster,
                [new GeneratedVideoRendition(plan, playlist)]));
        }
    }
}
