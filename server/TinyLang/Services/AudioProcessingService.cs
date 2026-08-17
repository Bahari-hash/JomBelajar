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
/// 通过 RabbitMQ 调度和 PostgreSQL 租约编排音频下载、探测、转码、上传和重试。
/// </summary>
public sealed class AudioProcessingService : IAudioProcessingService
{
    private const string ImmutableCacheControl = "public,max-age=31536000,immutable";
    private const int CleanupObjectLimit = 16;
    private readonly ApplicationDbContext _db;
    private readonly IObjectStorageService _objectStorage;
    private readonly IAudioProbe _audioProbe;
    private readonly IAudioTranscoder _audioTranscoder;
    private readonly AudioProcessingSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AudioProcessingService> _logger;

    /// <summary>
    /// 使用具体 EF 上下文、音频处理接口、存储和 worker 配置创建处理服务。
    /// </summary>
    public AudioProcessingService(
        ApplicationDbContext db,
        IObjectStorageService objectStorage,
        IAudioProbe audioProbe,
        IAudioTranscoder audioTranscoder,
        IOptions<AudioProcessingSettings> options,
        TimeProvider timeProvider,
        ILogger<AudioProcessingService> logger)
    {
        _db = db;
        _objectStorage = objectStorage;
        _audioProbe = audioProbe;
        _audioTranscoder = audioTranscoder;
        _settings = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AudioProcessingDispatchItem>> GetDispatchableJobsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var dispatchBefore = now.AddSeconds(-_settings.DispatchThrottleSeconds);
        return await _db.AudioProcessingJobs.AsNoTracking()
            .Where(job =>
                (job.AudioResource.Status == AudioResourceStatus.Queued ||
                    job.AudioResource.Status == AudioResourceStatus.Processing) &&
                ((job.Status == AudioProcessingJobStatus.Queued &&
                        (job.NextAttemptAt == null || job.NextAttemptAt <= now)) ||
                    (job.Status == AudioProcessingJobStatus.Processing &&
                        job.LeaseExpiresAt <= now)) &&
                (job.LastDispatchedAt == null ||
                    job.LastDispatchedAt <= dispatchBefore))
            .OrderBy(job => job.NextAttemptAt)
            .ThenBy(job => job.CreatedAt)
            .ThenBy(job => job.Id)
            .Select(job => new AudioProcessingDispatchItem(
                job.Id,
                job.AudioResourceId,
                job.OutputVersion))
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
        var job = await _db.AudioProcessingJobs
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || !CanClaim(job, now))
        {
            return;
        }
        // The message is already published; rotating the stamp here can invalidate its claim.
        job.LastDispatchedAt = now;
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
        Guid audioResourceId,
        Guid outputVersion,
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var job = await _db.AudioProcessingJobs
            .Include(value => value.AudioResource)
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null ||
            job.AudioResourceId != audioResourceId ||
            job.OutputVersion != outputVersion ||
            job.AudioResource is null ||
            job.AudioResource.Status is not (
                AudioResourceStatus.Queued or AudioResourceStatus.Processing) ||
            !CanClaim(job, now))
        {
            return false;
        }
        job.Status = AudioProcessingJobStatus.Processing;
        job.AttemptCount++;
        job.LeaseOwner = workerId;
        job.LeaseExpiresAt = now.AddSeconds(_settings.LeaseSeconds);
        job.StartedAt ??= now;
        job.FailureCode = null;
        job.ConcurrencyStamp = Guid.NewGuid();
        job.AudioResource.Status = AudioResourceStatus.Processing;
        job.AudioResource.LastFailureCode = null;
        job.AudioResource.ConcurrencyStamp = Guid.NewGuid();
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
        Guid audioResourceId,
        Guid outputVersion,
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        _db.ChangeTracker.Clear();
        var now = _timeProvider.GetUtcNow();
        var leaseExpiresAt = now.AddSeconds(_settings.LeaseSeconds);
        if (_db.Database.IsRelational())
        {
            var updated = await _db.AudioProcessingJobs
                .Where(job =>
                    job.Id == jobId &&
                    job.AudioResourceId == audioResourceId &&
                    job.OutputVersion == outputVersion &&
                    job.Status == AudioProcessingJobStatus.Processing &&
                    job.LeaseOwner == workerId &&
                    job.LeaseExpiresAt > now &&
                    job.AudioResource!.Status == AudioResourceStatus.Processing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.LeaseExpiresAt, leaseExpiresAt),
                    cancellationToken);
            return updated == 1;
        }

        var trackedJob = await _db.AudioProcessingJobs
            .SingleOrDefaultAsync(job =>
                job.Id == jobId &&
                job.AudioResourceId == audioResourceId &&
                job.OutputVersion == outputVersion &&
                job.Status == AudioProcessingJobStatus.Processing &&
                job.LeaseOwner == workerId &&
                job.LeaseExpiresAt > now &&
                job.AudioResource!.Status == AudioResourceStatus.Processing,
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
        Guid audioResourceId,
        Guid outputVersion,
        Guid workerId,
        CancellationToken cancellationToken = default)
    {
        string? temporaryDirectory = null;
        try
        {
            var job = await _db.AudioProcessingJobs
                .Include(value => value.AudioResource)
                    .ThenInclude(value => value!.SourceMediaResource)
                .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
            var now = _timeProvider.GetUtcNow();
            if (job is null || job.Status != AudioProcessingJobStatus.Processing ||
                job.AudioResourceId != audioResourceId ||
                job.OutputVersion != outputVersion ||
                job.AudioResource is null ||
                job.LeaseOwner != workerId || job.LeaseExpiresAt <= now)
            {
                return;
            }
            if (job.AudioResource.Status != AudioResourceStatus.Processing)
            {
                await SettleStaleJobAsync(job, cancellationToken);
                return;
            }
            ValidateSourceResource(job.AudioResource);

            temporaryDirectory = CreateTemporaryDirectory(job.Id);
            EnsureMinimumDiskSpace(temporaryDirectory);
            var sourcePath = Path.Combine(temporaryDirectory, "source.media");
            await DownloadSourceAsync(
                job.AudioResource.SourceMediaResource.ObjectName,
                sourcePath,
                cancellationToken);
            var sourceProbe = await _audioProbe.ProbeAsync(
                sourcePath,
                cancellationToken);

            var outputObjectName = GetOutputObjectName(
                job.AudioResourceId,
                job.OutputVersion);
            if (await TryRecoverStoredOutputAsync(
                outputObjectName,
                temporaryDirectory,
                sourceProbe,
                cancellationToken))
            {
                await MarkReadyAsync(job, sourceProbe, outputObjectName, cancellationToken);
                return;
            }

            var outputDirectory = Path.Combine(temporaryDirectory, "output");
            var output = await _audioTranscoder.TranscodeAsync(
                sourcePath,
                outputDirectory,
                cancellationToken);
            var outputProbe = await _audioProbe.ProbeAsync(
                output.OutputPath,
                cancellationToken);
            ValidateTranscodedOutput(sourceProbe, outputProbe);
            await UploadOutputAsync(
                job,
                output.OutputPath,
                outputObjectName,
                cancellationToken);
            await MarkReadyAsync(job, sourceProbe, outputObjectName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AudioProcessingException exception)
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
                "Unexpected audio processing failure for job {JobId} with {FailureType}",
                jobId,
                exception.GetType().Name);
            await RecordFailureAsync(
                jobId,
                workerId,
                AudioProcessingFailureCode.WorkerUnexpectedFailure,
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
    /// 验证处理时的 source 仍保持 Active Audio 状态。
    /// </summary>
    private static void ValidateSourceResource(AudioResource audioResource)
    {
        var source = audioResource.SourceMediaResource;
        if (source.Module != ResourceModule.Audio ||
            source.Status != ResourceStatus.Active)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.SourceResourceInvalid,
                isTransient: false);
        }
    }

    /// <summary>
    /// 将源对象流式下载到当前 job 独占的本地文件。
    /// </summary>
    private async Task DownloadSourceAsync(
        string objectName,
        string sourcePath,
        CancellationToken cancellationToken)
    {
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
                objectName,
                source,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.SourceDownloadFailed,
                isTransient: true);
        }
    }

    /// <summary>
    /// 下载并重新探测已存在的 MP3，仅在真实输出属性仍有效时恢复。
    /// </summary>
    private async Task<bool> TryRecoverStoredOutputAsync(
        string objectName,
        string temporaryDirectory,
        AudioProbeResult sourceProbe,
        CancellationToken cancellationToken)
    {
        ObjectStorageMetadata? metadata;
        try
        {
            metadata = await _objectStorage.GetObjectMetadataAsync(
                objectName,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.OutputUploadFailed,
                isTransient: true);
        }
        if (metadata is null ||
            metadata.Size <= 0 ||
            !string.Equals(
                metadata.ContentType,
                "audio/mpeg",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var recoveredPath = Path.Combine(temporaryDirectory, "recovered-audio.mp3");
        try
        {
            await using var output = new FileStream(
                recoveredPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await _objectStorage.DownloadObjectAsync(
                objectName,
                output,
                cancellationToken);
            if (output.Length != metadata.Size)
            {
                throw new IOException("Recovered audio output length did not match metadata.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.OutputUploadFailed,
                isTransient: true);
        }

        try
        {
            var outputProbe = await _audioProbe.ProbeAsync(
                recoveredPath,
                cancellationToken);
            ValidateTranscodedOutput(sourceProbe, outputProbe);
            return true;
        }
        catch (AudioProcessingException exception) when (!exception.IsTransient)
        {
            return false;
        }
        catch (AudioProcessingException exception) when (
            exception.FailureCode == AudioProcessingFailureCode.OutputValidationFailed)
        {
            return false;
        }
    }

    /// <summary>
    /// 验证本地转码结果符合固定 codec、采样率、声道和时长约束。
    /// </summary>
    private void ValidateTranscodedOutput(
        AudioProbeResult source,
        AudioProbeResult output)
    {
        if (!string.Equals(output.Codec, "mp3", StringComparison.OrdinalIgnoreCase) ||
            output.SampleRate != _settings.OutputSampleRate ||
            output.Channels != _settings.OutputChannels ||
            Math.Abs(output.DurationSeconds - source.DurationSeconds) > 2)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.OutputValidationFailed,
                isTransient: true);
        }
    }

    /// <summary>
    /// 流式上传单个 MP3 并验证对象存储返回的大小和 Content-Type。
    /// </summary>
    private async Task UploadOutputAsync(
        AudioProcessingJob job,
        string outputPath,
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                outputPath,
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
                    "audio/mpeg",
                    ImmutableCacheControl,
                    new Dictionary<string, string>
                    {
                        ["audio-resource-id"] = job.AudioResourceId.ToString("N"),
                        ["output-version"] = job.OutputVersion.ToString("N")
                    }),
                cancellationToken);
            var metadata = await _objectStorage.GetObjectMetadataAsync(
                objectName,
                cancellationToken);
            if (metadata is null ||
                metadata.Size != stream.Length ||
                metadata.Size <= 0 ||
                !string.Equals(
                    metadata.ContentType,
                    "audio/mpeg",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new AudioProcessingException(
                    AudioProcessingFailureCode.OutputValidationFailed,
                    isTransient: true);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AudioProcessingException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.OutputUploadFailed,
                isTransient: true);
        }
    }

    /// <summary>
    /// 在输出验证后原子写入真实媒体属性和 Ready 状态。
    /// </summary>
    private async Task MarkReadyAsync(
        AudioProcessingJob job,
        AudioProbeResult probe,
        string outputObjectName,
        CancellationToken cancellationToken)
    {
        if (job.AudioResource is null ||
            job.AudioResource.Status != AudioResourceStatus.Processing)
        {
            await SettleStaleJobAsync(job, cancellationToken);
            return;
        }
        job.AudioResource.DurationSeconds = probe.DurationSeconds;
        job.AudioResource.SampleRate = probe.SampleRate;
        job.AudioResource.Channels = probe.Channels;
        job.AudioResource.ContainerFormat = probe.ContainerFormat;
        job.AudioResource.SourceCodec = probe.Codec;
        job.AudioResource.CurrentOutputVersion = job.OutputVersion;
        job.AudioResource.OutputObjectName = outputObjectName;
        job.AudioResource.Status = AudioResourceStatus.Ready;
        job.AudioResource.LastFailureCode = null;
        job.AudioResource.ConcurrencyStamp = Guid.NewGuid();
        job.Status = AudioProcessingJobStatus.Completed;
        job.CompletedAt = _timeProvider.GetUtcNow();
        job.NextAttemptAt = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.FailureCode = null;
        job.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 重新加载任务并持久化自动重试或失败终态。
    /// </summary>
    private async Task RecordFailureAsync(
        Guid jobId,
        Guid workerId,
        AudioProcessingFailureCode failureCode,
        bool isTransient,
        CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var job = await _db.AudioProcessingJobs
            .Include(value => value.AudioResource)
            .SingleOrDefaultAsync(value => value.Id == jobId, cancellationToken);
        if (job is null || job.Status != AudioProcessingJobStatus.Processing ||
            job.AudioResource is null || job.LeaseOwner != workerId)
        {
            return;
        }
        if (job.AudioResource.Status != AudioResourceStatus.Processing)
        {
            await SettleStaleJobAsync(job, cancellationToken);
            return;
        }

        var reachesTerminal = !isTransient || job.AttemptCount >= _settings.MaxAttempts;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.FailureCode = failureCode.ToString();
        job.ConcurrencyStamp = Guid.NewGuid();
        job.AudioResource.LastFailureCode = failureCode.ToString();
        job.AudioResource.ConcurrencyStamp = Guid.NewGuid();
        if (reachesTerminal)
        {
            job.Status = AudioProcessingJobStatus.Failed;
            job.CompletedAt = _timeProvider.GetUtcNow();
            job.NextAttemptAt = null;
            job.AudioResource.Status = AudioResourceStatus.Failed;
        }
        else
        {
            job.Status = AudioProcessingJobStatus.Queued;
            var delayMinutes = Math.Min(
                Math.Pow(2, job.AttemptCount - 1),
                _settings.RetryMaxDelayMinutes);
            job.NextAttemptAt = _timeProvider.GetUtcNow().AddMinutes(delayMinutes);
            job.AudioResource.Status = AudioResourceStatus.Queued;
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
            "Audio job {JobId} attempt {AttemptCount} failed with {FailureCode}; terminal: {Terminal}",
            job.Id,
            job.AttemptCount,
            failureCode,
            reachesTerminal);
    }

    /// <summary>
    /// 收敛已经被资源状态或输出版本淘汰的任务，避免重复消息再次执行。
    /// </summary>
    private async Task SettleStaleJobAsync(
        AudioProcessingJob job,
        CancellationToken cancellationToken)
    {
        job.Status = AudioProcessingJobStatus.Failed;
        job.CompletedAt = _timeProvider.GetUtcNow();
        job.NextAttemptAt = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.FailureCode = AudioProcessingFailureCode.OutputVersionSuperseded.ToString();
        job.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return;
        }
        await CleanupFailedOutputAsync(job, cancellationToken);
    }

    /// <summary>
    /// 有界且只针对当前 AudioResourceId/OutputVersion 前缀清理失败输出。
    /// </summary>
    private async Task CleanupFailedOutputAsync(
        AudioProcessingJob job,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = GetOutputPrefix(job.AudioResourceId, job.OutputVersion);
            var objectNames = await _objectStorage.ListObjectNamesAsync(
                prefix,
                CleanupObjectLimit,
                cancellationToken);
            if (objectNames.Any(value =>
                !value.StartsWith(prefix, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Storage returned an object outside the audio cleanup prefix.");
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
                "Audio failed output cleanup for job {JobId} returned {FailureType}",
                job.Id,
                exception.GetType().Name);
        }
    }

    /// <summary>
    /// 判断任务是否为到期 queued 或租约已过期的 processing 状态。
    /// </summary>
    private static bool CanClaim(AudioProcessingJob job, DateTimeOffset now)
        => (job.Status == AudioProcessingJobStatus.Queued &&
                (job.NextAttemptAt is null || job.NextAttemptAt <= now)) ||
            (job.Status == AudioProcessingJobStatus.Processing &&
                job.LeaseExpiresAt <= now);

    /// <summary>
    /// 创建并验证当前 job 独占的临时目录位于配置根目录内。
    /// </summary>
    private string CreateTemporaryDirectory(Guid jobId)
    {
        try
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
                throw new IOException("Audio temporary path escaped its configured root.");
            }
            Directory.CreateDirectory(directory);
            return directory;
        }
        catch (Exception exception) when (exception is not AudioProcessingException)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.TemporaryStorageUnavailable,
                isTransient: true);
        }
    }

    /// <summary>
    /// 在开始下载前检查临时目录所在卷的最小剩余空间。
    /// </summary>
    private void EnsureMinimumDiskSpace(string temporaryDirectory)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(temporaryDirectory));
            if (root is null ||
                new DriveInfo(root).AvailableFreeSpace <
                    (long)_settings.MinimumFreeDiskMB * 1024 * 1024)
            {
                throw new IOException("Insufficient audio worker disk space.");
            }
        }
        catch (Exception exception) when (exception is not AudioProcessingException)
        {
            throw new AudioProcessingException(
                AudioProcessingFailureCode.TemporaryStorageUnavailable,
                isTransient: true);
        }
    }

    /// <summary>
    /// 构建只包含服务端标识的不可变音频输出前缀。
    /// </summary>
    private static string GetOutputPrefix(Guid audioResourceId, Guid outputVersion)
        => $"audios/{audioResourceId:N}/outputs/{outputVersion:N}/";

    /// <summary>
    /// 构建当前不可变版本唯一允许的 MP3 对象名称。
    /// </summary>
    private static string GetOutputObjectName(Guid audioResourceId, Guid outputVersion)
        => $"{GetOutputPrefix(audioResourceId, outputVersion)}audio.mp3";

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
                "Audio temporary directory cleanup failed with {FailureType}",
                exception.GetType().Name);
        }
    }
}
