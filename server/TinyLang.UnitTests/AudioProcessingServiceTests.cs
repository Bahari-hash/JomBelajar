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
/// 验证音频任务调度、租约、失败分类、输出发布和恢复语义。
/// </summary>
public sealed class AudioProcessingServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证到期任务领取时设置租约、attempt 和音频 Processing 状态。
    /// </summary>
    [Fact]
    public async Task DispatchAndClaimShouldSetLeaseAndProcessingState()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedAudio(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var service = CreateService(db);

        var dispatchable = await service.GetDispatchableJobIdsAsync(
            TestContext.Current.CancellationToken);
        await service.MarkDispatchedAsync(job.Id, TestContext.Current.CancellationToken);
        var claimed = await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);
        var stored = await db.AudioProcessingJobs.AsNoTracking()
            .Include(value => value.AudioClip)
            .SingleAsync(TestContext.Current.CancellationToken);

        dispatchable.Should().ContainSingle().Which.Should().Be(job.Id);
        claimed.Should().BeTrue();
        stored.LastDispatchedAt.Should().Be(Now);
        stored.AttemptCount.Should().Be(1);
        stored.LeaseOwner.Should().Be(workerId);
        stored.AudioClip.ProcessingStatus.Should().Be(AudioProcessingStatus.Processing);
    }

    /// <summary>
    /// 验证有效租约存在时重复消息不能并发领取同一音频任务。
    /// </summary>
    [Fact]
    public async Task DuplicateClaimShouldBeIgnoredWhileLeaseIsActive()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedAudio(db);
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

    /// <summary>
    /// 验证永久媒体失败不进入自动重试并有界清理当前版本前缀。
    /// </summary>
    [Fact]
    public async Task PermanentProbeFailureShouldBecomeTerminal()
    {
        await using var db = CreateDbContext();
        var (audioClip, job) = AddQueuedAudio(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var probe = new Mock<IAudioProbe>();
        probe.Setup(value => value.ProbeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AudioProcessingException(
                AudioProcessingFailureCode.VideoStreamPresent,
                isTransient: false));
        var storage = CreateStorageForDownload();
        storage.Setup(value => value.ListObjectNamesAsync(
                It.IsAny<string>(),
                16,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        storage.Setup(value => value.DeleteObjectsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(db, storage.Object, probe.Object);
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        var storedJob = await db.AudioProcessingJobs.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        var storedAudio = await db.AudioClips.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedJob.Status.Should().Be(AudioProcessingJobStatus.Failed);
        storedAudio.ProcessingStatus.Should().Be(AudioProcessingStatus.Failed);
        storedAudio.LastFailureCode.Should().Be("VideoStreamPresent");
        storage.Verify(value => value.ListObjectNamesAsync(
            $"audios/{audioClip.Id:N}/outputs/{job.OutputVersion:N}/",
            16,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证临时下载故障回到 queued 并按第一次 attempt 延迟一分钟。
    /// </summary>
    [Fact]
    public async Task TransientDownloadFailureShouldScheduleRetry()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedAudio(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.DownloadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("temporary"));
        var service = CreateService(db, storage.Object);
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        var storedJob = await db.AudioProcessingJobs.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedJob.Status.Should().Be(AudioProcessingJobStatus.Queued);
        storedJob.NextAttemptAt.Should().Be(Now.AddMinutes(1));
        storedJob.FailureCode.Should().Be("SourceDownloadFailed");
    }

    /// <summary>
    /// 验证临时失败达到最大 attempts 后进入终态而不再调度。
    /// </summary>
    [Fact]
    public async Task TransientFailureAtAttemptLimitShouldBecomeTerminal()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedAudio(db);
        job.AttemptCount = 2;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.DownloadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("temporary"));
        storage.Setup(value => value.ListObjectNamesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        storage.Setup(value => value.DeleteObjectsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(db, storage.Object);
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        var storedJob = await db.AudioProcessingJobs.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedJob.AttemptCount.Should().Be(3);
        storedJob.Status.Should().Be(AudioProcessingJobStatus.Failed);
        storedJob.NextAttemptAt.Should().BeNull();
    }

    /// <summary>
    /// 验证本地输出校验后以 MP3 metadata 上传并最终置为 Ready。
    /// </summary>
    [Fact]
    public async Task SuccessfulProcessingShouldUploadValidatedMp3BeforeReady()
    {
        await using var db = CreateDbContext();
        var (audioClip, job) = AddQueuedAudio(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        long? uploadedLength = null;
        ObjectStorageUploadOptions? uploadOptions = null;
        var storage = CreateStorageForDownload();
        storage.Setup(value => value.UploadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                It.IsAny<ObjectStorageUploadOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Stream, long, ObjectStorageUploadOptions, CancellationToken>(
                (_, _, length, options, _) =>
                {
                    uploadedLength = length;
                    uploadOptions = options;
                })
            .Returns(Task.CompletedTask);
        storage.Setup(value => value.GetObjectMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken _) =>
                Task.FromResult<ObjectStorageMetadata?>(uploadedLength is null
                    ? null
                    : new ObjectStorageMetadata(uploadedLength.Value, "audio/mpeg")));
        var probe = new Mock<IAudioProbe>();
        probe.SetupSequence(value => value.ProbeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioProbeResult(4, 48000, 2, "wav", "pcm_s16le"))
            .ReturnsAsync(new AudioProbeResult(4.02, 44100, 1, "mp3", "mp3"));
        var service = CreateService(
            db,
            storage.Object,
            probe.Object,
            new FakeAudioTranscoder());
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        var storedAudio = await db.AudioClips.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        storedAudio.ProcessingStatus.Should().Be(AudioProcessingStatus.Ready);
        storedAudio.CurrentOutputVersion.Should().Be(job.OutputVersion);
        storedAudio.OutputObjectName.Should().Be(
            $"audios/{audioClip.Id:N}/outputs/{job.OutputVersion:N}/audio.mp3");
        uploadOptions!.ContentType.Should().Be("audio/mpeg");
        uploadOptions.CacheControl.Should().Be("public,max-age=31536000,immutable");
    }

    /// <summary>
    /// 验证已存在有效版本对象时跳过转码和覆盖并恢复 Ready 状态。
    /// </summary>
    [Fact]
    public async Task ExistingValidOutputShouldRecoverWithoutRetranscoding()
    {
        await using var db = CreateDbContext();
        var (_, job) = AddQueuedAudio(db);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();
        var storage = CreateStorageForDownload();
        storage.Setup(value => value.DownloadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, Stream destination, CancellationToken _) =>
            {
                destination.Write(new byte[128]);
                return Task.CompletedTask;
            });
        storage.Setup(value => value.GetObjectMetadataAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(128, "audio/mpeg"));
        var probe = new Mock<IAudioProbe>();
        probe.SetupSequence(value => value.ProbeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AudioProbeResult(3, 48000, 2, "wav", "pcm_s16le"))
            .ReturnsAsync(new AudioProbeResult(3.01, 44100, 1, "mp3", "mp3"));
        var transcoder = new Mock<IAudioTranscoder>();
        var service = CreateService(db, storage.Object, probe.Object, transcoder.Object);
        (await service.TryClaimAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        await service.ProcessClaimedAsync(
            job.Id,
            workerId,
            TestContext.Current.CancellationToken);

        (await db.AudioClips.AsNoTracking().SingleAsync(
            TestContext.Current.CancellationToken)).ProcessingStatus
            .Should().Be(AudioProcessingStatus.Ready);
        transcoder.Verify(value => value.TranscodeAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(value => value.UploadObjectAsync(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<long>(),
            It.IsAny<ObjectStorageUploadOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 使用可替换存储、探测和转码实现创建处理服务。
    /// </summary>
    private static AudioProcessingService CreateService(
        ApplicationDbContext db,
        IObjectStorageService? storage = null,
        IAudioProbe? probe = null,
        IAudioTranscoder? transcoder = null)
        => new(
            db,
            storage ?? Mock.Of<IObjectStorageService>(),
            probe ?? Mock.Of<IAudioProbe>(),
            transcoder ?? Mock.Of<IAudioTranscoder>(),
            Options.Create(new AudioProcessingSettings
            {
                TemporaryDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "tiny-lang-audio-tests"),
                MinimumFreeDiskMB = 1
            }),
            new TestTimeProvider(Now),
            NullLogger<AudioProcessingService>.Instance);

    /// <summary>
    /// 创建能够完成源下载的对象存储 mock。
    /// </summary>
    private static Mock<IObjectStorageService> CreateStorageForDownload()
    {
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.DownloadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return storage;
    }

    /// <summary>
    /// 添加一个 Active Audio source、音频草稿和 queued job。
    /// </summary>
    private static (AudioClip AudioClip, AudioProcessingJob Job) AddQueuedAudio(
        ApplicationDbContext db)
    {
        var resource = new MediaResource
        {
            UploaderId = Guid.NewGuid(),
            ObjectName = "audios/source.wav",
            OriginalName = "source.wav",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".wav",
            ContentType = "audio/wav"
        };
        var audioClip = new AudioClip
        {
            CreatedById = resource.UploaderId,
            SourceMediaResourceId = resource.Id,
            SourceMediaResource = resource,
            Title = "Audio",
            LanguageTag = "en",
            Kind = AudioClipKind.Other
        };
        var job = new AudioProcessingJob
        {
            AudioClipId = audioClip.Id,
            AudioClip = audioClip,
            OutputVersion = Guid.NewGuid()
        };
        audioClip.ProcessingJobs.Add(job);
        db.Add(audioClip);
        return (audioClip, job);
    }

    /// <summary>
    /// 创建隔离的 EF Core InMemory 上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// 在 job 输出目录创建一个非空 MP3 占位文件。
    /// </summary>
    private sealed class FakeAudioTranscoder : IAudioTranscoder
    {
        /// <inheritdoc />
        public Task<AudioTranscodeResult> TranscodeAsync(
            string inputPath,
            string outputDirectory,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "audio.mp3");
            File.WriteAllText(outputPath, "mp3");
            return Task.FromResult(new AudioTranscodeResult(outputPath));
        }
    }
}
