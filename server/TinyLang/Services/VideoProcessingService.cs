using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 通过 RabbitMQ 调度和 PostgreSQL 租约编排源下载、ffprobe、FFmpeg、顺序上传和失败重试。
/// </summary>
public sealed class VideoProcessingService : IVideoProcessingService
{
    private const string ImmutableCacheControl = "public,max-age=31536000,immutable";
    private readonly ApplicationDbContext _db;
    private readonly IObjectStorageService _objectStorage;
    private readonly IMediaProbe _mediaProbe;
    private readonly IVideoTranscoder _videoTranscoder;
    private readonly VideoProcessingSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VideoProcessingService> _logger;

    /// <summary>
    /// 使用具体 EF 上下文、媒体处理接口、存储和 worker 配置创建处理服务。
    /// </summary>
    public VideoProcessingService(
        ApplicationDbContext db,
        IObjectStorageService objectStorage,
        IMediaProbe mediaProbe,
        IVideoTranscoder videoTranscoder,
        IOptions<VideoProcessingSettings> options,
        TimeProvider timeProvider,
        ILogger<VideoProcessingService> logger)
    {
        _db = db;
        _objectStorage = objectStorage;
        _mediaProbe = mediaProbe;
        _videoTranscoder = videoTranscoder;
        _settings = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetDispatchableJobIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var dispatchBefore = now.AddSeconds(-_settings.DispatchThrottleSeconds);
        return await _db.VideoProcessingJobs.AsNoTracking()
            .Where(job =>
                ((job.Status == VideoProcessingJobStatus.Queued &&
                        (job.NextAttemptAt == null || job.NextAttemptAt <= now)) ||
                    (job.Status == VideoProcessingJobStatus.Processing &&
                        job.LeaseExpiresAt <= now)) &&
                (job.LastDispatchedAt == null ||
                    job.LastDispatchedAt <= dispatchBefore))
            .OrderBy(job => job.NextAttemptAt)
            .ThenBy(job => job.CreatedAt)
            .ThenBy(job => job.Id)
            .Select(job => job.Id)
            .Take(_settings.BatchSize)
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkDispatchedAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var job = await _db.VideoProcessingJobs
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || !CanClaim(job, now))
        {
            return;
        }
        job.LastDispatchedAt = now;
        job.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
        }
        _db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<bool> TryClaimAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var job = await _db.VideoProcessingJobs
            .Include(value => value.Video)
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || !CanClaim(job, now))
        {
            return false;
        }
        job.Status = VideoProcessingJobStatus.Processing;
        job.AttemptCount++;
        job.LeaseOwner = workerId;
        job.LeaseExpiresAt = now.AddSeconds(_settings.LeaseSeconds);
        job.StartedAt ??= now;
        job.FailureCode = null;
        job.ConcurrencyStamp = Guid.NewGuid();
        job.Video.ProcessingStatus = VideoProcessingStatus.Processing;
        job.Video.LastFailureCode = null;
        job.Video.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        finally
        {
            _db.ChangeTracker.Clear();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RenewLeaseAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var leaseExpiresAt = now.AddSeconds(_settings.LeaseSeconds);
        if (_db.Database.IsRelational())
        {
            var updated = await _db.VideoProcessingJobs
                .Where(job =>
                    job.Id == jobId &&
                    job.Status == VideoProcessingJobStatus.Processing &&
                    job.LeaseOwner == workerId &&
                    job.LeaseExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.LeaseExpiresAt, leaseExpiresAt),
                    cancellationToken);
            return updated == 1;
        }

        // The non-relational path preserves lease behavior in isolated tests;
        // production PostgreSQL uses the conditional update above.
        var trackedJob = await _db.VideoProcessingJobs
            .SingleOrDefaultAsync(job =>
                job.Id == jobId &&
                job.Status == VideoProcessingJobStatus.Processing &&
                job.LeaseOwner == workerId &&
                job.LeaseExpiresAt > now,
                cancellationToken);
        if (trackedJob is null)
        {
            return false;
        }

        trackedJob.LeaseExpiresAt = leaseExpiresAt;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        finally
        {
            _db.ChangeTracker.Clear();
        }
    }

    /// <inheritdoc />
    public async Task ProcessClaimedAsync(
        Guid jobId,
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        string? temporaryDirectory = null;
        try
        {
            var job = await _db.VideoProcessingJobs
                .Include(value => value.Video)
                    .ThenInclude(value => value.SourceMediaResource)
                .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
            var now = _timeProvider.GetUtcNow();
            if (job is null || job.Status != VideoProcessingJobStatus.Processing ||
                job.LeaseOwner != workerId || job.LeaseExpiresAt <= now)
            {
                return;
            }

            temporaryDirectory = CreateTemporaryDirectory(job.Id);
            EnsureMinimumDiskSpace(temporaryDirectory);
            var sourcePath = Path.Combine(temporaryDirectory, "source.media");
            try
            {
                await using var source = new FileStream(
                    sourcePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await _objectStorage.DownloadObjectAsync(
                    job.Video.SourceMediaResource.ObjectName,
                    source,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new VideoProcessingException(
                    VideoProcessingFailureCode.SourceDownloadFailed,
                    isTransient: true);
            }

            var probe = await _mediaProbe.ProbeAsync(sourcePath, cancellationToken);
            var outputDirectory = Path.Combine(temporaryDirectory, "output");
            var output = await _videoTranscoder.TranscodeAsync(
                sourcePath,
                outputDirectory,
                probe,
                cancellationToken);
            await UploadOutputAsync(job, outputDirectory, output, cancellationToken);
            await MarkReadyAsync(
                jobId,
                workerId,
                probe,
                output,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VideoProcessingException exception)
        {
            await RecordFailureAsync(
                jobId,
                workerId,
                exception.FailureCode,
                exception.IsTransient,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Unexpected video processing failure for job {JobId} with {FailureType}",
                jobId,
                exception.GetType().Name);
            await RecordFailureAsync(
                jobId,
                workerId,
                VideoProcessingFailureCode.WorkerUnexpectedFailure,
                isTransient: true,
                cancellationToken);
        }
        finally
        {
            if (temporaryDirectory is not null)
            {
                TryDeleteTemporaryDirectory(temporaryDirectory);
            }
        }
    }

    /// <summary>
    /// 按 segments、variant playlists、poster、master 的顺序上传输出。
    /// </summary>
    private async Task UploadOutputAsync(
        VideoProcessingJob job,
        string outputDirectory,
        VideoTranscodeResult output,
        CancellationToken cancellationToken)
    {
        var prefix = GetOutputPrefix(job.VideoId, job.OutputVersion);
        var renditionFiles = output.Renditions
            .SelectMany(value => Directory.EnumerateFiles(
                Path.GetDirectoryName(value.PlaylistPath)!,
                "*",
                SearchOption.TopDirectoryOnly))
            .ToArray();
        var segments = renditionFiles
            .Where(path => string.Equals(Path.GetExtension(path), ".ts", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var playlists = output.Renditions
            .Select(value => value.PlaylistPath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        try
        {
            foreach (var path in segments)
            {
                await UploadFileAsync(
                    outputDirectory,
                    path,
                    prefix,
                    "video/mp2t",
                    job,
                    cancellationToken);
            }
            foreach (var path in playlists)
            {
                await UploadFileAsync(
                    outputDirectory,
                    path,
                    prefix,
                    "application/vnd.apple.mpegurl",
                    job,
                    cancellationToken);
            }
            await UploadFileAsync(
                outputDirectory,
                output.PosterPath,
                prefix,
                "image/jpeg",
                job,
                cancellationToken);
            await UploadFileAsync(
                outputDirectory,
                output.MasterPlaylistPath,
                prefix,
                "application/vnd.apple.mpegurl",
                job,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VideoProcessingException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.OutputUploadFailed,
                isTransient: true);
        }

        var masterObjectName = ToObjectName(
            outputDirectory,
            output.MasterPlaylistPath,
            prefix);
        var masterMetadata = await _objectStorage.GetObjectMetadataAsync(
            masterObjectName,
            cancellationToken);
        if (masterMetadata is null ||
            masterMetadata.Size <= 0 ||
            !string.Equals(
                masterMetadata.ContentType,
                "application/vnd.apple.mpegurl",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.OutputValidationFailed,
                isTransient: true);
        }
    }

    /// <summary>
    /// 流式上传一个本地输出文件并附加可对账的通用 metadata。
    /// </summary>
    private async Task UploadFileAsync(
        string outputDirectory,
        string path,
        string prefix,
        string contentType,
        VideoProcessingJob job,
        CancellationToken cancellationToken)
    {
        var objectName = ToObjectName(outputDirectory, path, prefix);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await _objectStorage.UploadObjectAsync(
            objectName,
            stream,
            stream.Length,
            new ObjectStorageUploadOptions(
                contentType,
                ImmutableCacheControl,
                new Dictionary<string, string>
                {
                    ["video-id"] = job.VideoId.ToString("N"),
                    ["output-version"] = job.OutputVersion.ToString("N")
                }),
            cancellationToken);
    }

    /// <summary>
    /// 在 master 已验证后原子写入媒体属性、renditions 和 Ready 状态。
    /// </summary>
    private async Task<bool> MarkReadyAsync(
        Guid jobId,
        Guid workerId,
        MediaProbeResult probe,
        VideoTranscodeResult output,
        CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var job = await _db.VideoProcessingJobs
            .Include(value => value.Video)
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || job.Status != VideoProcessingJobStatus.Processing ||
            job.LeaseOwner != workerId || job.LeaseExpiresAt <= now)
        {
            return false;
        }

        var prefix = GetOutputPrefix(job.VideoId, job.OutputVersion);
        job.Video.DurationSeconds = probe.DurationSeconds;
        job.Video.DisplayWidth = probe.DisplayWidth;
        job.Video.DisplayHeight = probe.DisplayHeight;
        job.Video.ContainerFormat = probe.ContainerFormat;
        job.Video.VideoCodec = probe.VideoCodec;
        job.Video.AudioCodec = probe.AudioCodec;
        job.Video.CurrentOutputVersion = job.OutputVersion;
        job.Video.MasterPlaylistObjectName = ToObjectName(
            Path.GetDirectoryName(output.MasterPlaylistPath)!,
            output.MasterPlaylistPath,
            prefix);
        job.Video.PosterObjectName = ToObjectName(
            Path.GetDirectoryName(output.PosterPath)!,
            output.PosterPath,
            prefix);
        job.Video.ProcessingStatus = VideoProcessingStatus.Ready;
        job.Video.LastFailureCode = null;
        job.Video.ConcurrencyStamp = Guid.NewGuid();
        foreach (var rendition in output.Renditions)
        {
            _db.VideoRenditions.Add(new VideoRendition
            {
                VideoId = job.VideoId,
                OutputVersion = job.OutputVersion,
                TargetHeight = rendition.Plan.TargetHeight,
                Width = rendition.Plan.Width,
                Height = rendition.Plan.Height,
                VideoBitrateKbps = rendition.Plan.VideoBitrateKbps,
                AudioBitrateKbps = rendition.Plan.AudioBitrateKbps,
                PlaylistObjectName = $"{prefix}{rendition.Plan.Label}/index.m3u8",
                Codecs = rendition.Plan.Codecs
            });
        }
        job.Status = VideoProcessingJobStatus.Completed;
        job.CompletedAt = now;
        job.NextAttemptAt = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.FailureCode = null;
        job.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    /// <summary>
    /// 清除失败跟踪状态后重新加载任务并持久化自动重试或失败终态。
    /// </summary>
    private async Task RecordFailureAsync(
        Guid jobId,
        Guid workerId,
        VideoProcessingFailureCode failureCode,
        bool isTransient,
        CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var job = await _db.VideoProcessingJobs
            .Include(value => value.Video)
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || job.Status != VideoProcessingJobStatus.Processing ||
            job.LeaseOwner != workerId || job.LeaseExpiresAt <= now)
        {
            return;
        }

        var reachesTerminal = !isTransient || job.AttemptCount >= _settings.MaxAttempts;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.FailureCode = failureCode.ToString();
        job.ConcurrencyStamp = Guid.NewGuid();
        job.Video.LastFailureCode = failureCode.ToString();
        job.Video.ConcurrencyStamp = Guid.NewGuid();
        if (reachesTerminal)
        {
            job.Status = VideoProcessingJobStatus.Failed;
            job.CompletedAt = now;
            job.NextAttemptAt = null;
            job.Video.ProcessingStatus = VideoProcessingStatus.Failed;
        }
        else
        {
            job.Status = VideoProcessingJobStatus.Queued;
            var delayMinutes = Math.Min(
                Math.Pow(2, job.AttemptCount - 1),
                _settings.RetryMaxDelayMinutes);
            job.NextAttemptAt = now.AddMinutes(delayMinutes);
            job.Video.ProcessingStatus = VideoProcessingStatus.Queued;
        }
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return;
        }

        if (reachesTerminal)
        {
            await CleanupFailedOutputAsync(job, cancellationToken);
        }
        _logger.LogWarning(
            "Video job {JobId} attempt {AttemptCount} failed with {FailureCode}; terminal: {Terminal}",
            job.Id,
            job.AttemptCount,
            failureCode,
            reachesTerminal);
    }

    /// <summary>
    /// 有界且只针对当前 VideoId/OutputVersion 前缀清理失败输出。
    /// </summary>
    private async Task CleanupFailedOutputAsync(
        VideoProcessingJob job,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = GetOutputPrefix(job.VideoId, job.OutputVersion);
            var objectNames = await _objectStorage.ListObjectNamesAsync(
                prefix,
                10000,
                cancellationToken);
            if (objectNames.Any(value =>
                !value.StartsWith(prefix, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("Storage returned an object outside the cleanup prefix.");
            }
            await _objectStorage.DeleteObjectsAsync(objectNames, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Failed output cleanup for job {JobId} returned {FailureType}",
                job.Id,
                exception.GetType().Name);
        }
    }

    /// <summary>
    /// 判断任务是否为到期 queued 或租约已过期的 processing 状态。
    /// </summary>
    private static bool CanClaim(VideoProcessingJob job, DateTimeOffset now)
        => (job.Status == VideoProcessingJobStatus.Queued &&
                (job.NextAttemptAt is null || job.NextAttemptAt <= now)) ||
            (job.Status == VideoProcessingJobStatus.Processing &&
                job.LeaseExpiresAt <= now);

    /// <summary>
    /// 创建并验证当前 job 独占的临时目录位于配置根目录内。
    /// </summary>
    private string CreateTemporaryDirectory(Guid jobId)
    {
        var root = Path.GetFullPath(_settings.TemporaryDirectory);
        Directory.CreateDirectory(root);
        var directory = Path.GetFullPath(Path.Combine(
            root,
            $"{jobId:N}-{Guid.NewGuid():N}"));
        if (!directory.StartsWith(
            root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.TemporaryStorageUnavailable,
                isTransient: true);
        }
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    /// 在开始下载前检查临时目录所在卷的最小剩余空间。
    /// </summary>
    private void EnsureMinimumDiskSpace(string temporaryDirectory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(temporaryDirectory));
        if (root is null ||
            new DriveInfo(root).AvailableFreeSpace <
                (long)_settings.MinimumFreeDiskMB * 1024 * 1024)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.TemporaryStorageUnavailable,
                isTransient: true);
        }
    }

    /// <summary>
    /// 将本地输出路径安全映射为当前不可变输出版本的对象名称。
    /// </summary>
    private static string ToObjectName(
        string outputDirectory,
        string path,
        string prefix)
    {
        var relative = Path.GetRelativePath(outputDirectory, path).Replace('\\', '/');
        if (relative.StartsWith("../", StringComparison.Ordinal) || relative == "..")
        {
            throw new InvalidOperationException("Output path escaped its job directory.");
        }
        return $"{prefix}{relative}";
    }

    /// <summary>
    /// 构建只包含服务端 GUID 的不可变输出对象前缀。
    /// </summary>
    private static string GetOutputPrefix(Guid videoId, Guid outputVersion)
        => $"videos/{videoId:N}/outputs/{outputVersion:N}/";

    /// <summary>
    /// 尽力清理已经验证位于 worker 临时根目录内的 job 目录。
    /// </summary>
    private void TryDeleteTemporaryDirectory(string directory)
    {
        try
        {
            var root = Path.GetFullPath(_settings.TemporaryDirectory)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(directory);
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Video temporary directory cleanup failed with {FailureType}",
                exception.GetType().Name);
        }
    }
}
