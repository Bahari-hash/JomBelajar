using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 使用数据库租约分批归档 Multipart 对象，并清理过期或遗留上传对象。
/// </summary>
public sealed class MediaUploadMaintenanceService : IMediaUploadMaintenanceService
{
    private readonly IApplicationDbContext _db;
    private readonly IObjectStorageService _objectStorage;
    private readonly MultipartUploadSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MediaUploadMaintenanceService> _logger;

    /// <summary>
    /// 创建可由后台 worker 周期调用的媒体上传维护服务。
    /// </summary>
    public MediaUploadMaintenanceService(
        IApplicationDbContext db,
        IObjectStorageService objectStorage,
        IOptions<MultipartUploadSettings> options,
        TimeProvider timeProvider,
        ILogger<MediaUploadMaintenanceService> logger)
    {
        _db = db;
        _objectStorage = objectStorage;
        _settings = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> FinalizeBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var sessionIds = await _db.MultipartUploadSessions
            .AsNoTracking()
            .Where(session =>
                session.Status == MultipartUploadStatus.Finalizing &&
                (session.NextAttemptAt == null || session.NextAttemptAt <= now) &&
                (session.LeaseExpiresAt == null || session.LeaseExpiresAt <= now))
            .OrderBy(session => session.NextAttemptAt)
            .ThenBy(session => session.CreatedAt)
            .ThenBy(session => session.Id)
            .Select(session => session.Id)
            .Take(_settings.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);

        var completed = 0;
        foreach (var sessionId in sessionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await TryFinalizeAsync(sessionId, cancellationToken))
            {
                completed++;
            }
        }
        return completed;
    }

    /// <inheritdoc />
    public async Task<int> CleanupBatchAsync(CancellationToken cancellationToken = default)
    {
        var cleaned = await CleanupExpiredMultipartAsync(cancellationToken);
        cleaned += await CleanupExpiredSimpleUploadsAsync(cancellationToken);
        cleaned += await CleanupActiveStagingObjectsAsync(cancellationToken);
        return cleaned;
    }

    /// <summary>
    /// 尝试取得数据库租约并将一个 Multipart staging 对象归档到最终 key。
    /// </summary>
    private async Task<bool> TryFinalizeAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var leaseOwner = Guid.NewGuid();
        var session = await _db.MultipartUploadSessions
            .Include(value => value.MediaResource)
            .SingleOrDefaultAsync(value => value.Id == sessionId, cancellationToken);
        if (session is null ||
            session.Status != MultipartUploadStatus.Finalizing ||
            session.LeaseExpiresAt > now)
        {
            return false;
        }
        session.LeaseOwner = leaseOwner;
        session.LeaseExpiresAt = now.AddSeconds(_settings.FinalizationLeaseSeconds);
        session.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        try
        {
            await FinalizeObjectAsync(session, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await RecordFinalizationFailureAsync(session, cancellationToken);
            _logger.LogWarning(
                "Media upload finalization failed for session {SessionId} on attempt {AttemptCount}: {FailureType}",
                session.Id,
                session.AttemptCount,
                exception.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// 通过 staging/final metadata 对账，完成复制、验证、删除和资源激活。
    /// </summary>
    private async Task FinalizeObjectAsync(
        MultipartUploadSession session,
        CancellationToken cancellationToken)
    {
        var resource = session.MediaResource;
        var stagingObjectName = resource.StagingObjectName
            ?? throw new InvalidOperationException("Finalizing resource has no staging object.");
        var finalMetadata = await _objectStorage.GetObjectMetadataAsync(
            resource.ObjectName,
            cancellationToken);
        if (finalMetadata is null)
        {
            var stagingMetadata = await _objectStorage.GetObjectMetadataAsync(
                stagingObjectName,
                cancellationToken)
                ?? throw new InvalidOperationException("Multipart staging object is missing.");
            ValidateObjectMetadata(resource, stagingMetadata);
            await _objectStorage.CopyObjectAsync(
                stagingObjectName,
                resource.ObjectName,
                cancellationToken);
            finalMetadata = await _objectStorage.GetObjectMetadataAsync(
                resource.ObjectName,
                cancellationToken)
                ?? throw new InvalidOperationException("Final object is missing after copy.");
        }
        ValidateObjectMetadata(resource, finalMetadata);

        var stagingCleanupRequired = false;
        try
        {
            await _objectStorage.DeleteObjectAsync(stagingObjectName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stagingCleanupRequired = true;
        }

        var now = _timeProvider.GetUtcNow();
        var publicUrl = _objectStorage.GetPublicUrl(resource.ObjectName);
        resource.Status = ResourceStatus.Active;
        resource.Url = publicUrl;
        resource.UploadExpiresAt = null;
        resource.StagingObjectName = stagingCleanupRequired ? stagingObjectName : null;
        resource.ConcurrencyStamp = Guid.NewGuid();
        session.Status = MultipartUploadStatus.Completed;
        session.CompletedAt = now;
        session.NextAttemptAt = null;
        session.LeaseOwner = null;
        session.LeaseExpiresAt = null;
        session.StagingCleanupRequired = stagingCleanupRequired;
        session.LastFailureCode = null;
        session.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 持久化一次归档失败，并在达到上限时将资源和会话置为失败终态。
    /// </summary>
    private async Task RecordFinalizationFailureAsync(
        MultipartUploadSession session,
        CancellationToken cancellationToken)
    {
        session.AttemptCount++;
        session.LeaseOwner = null;
        session.LeaseExpiresAt = null;
        session.LastFailureCode = ErrorCodes.ObjectStorageUnavailable.ToString();
        session.ConcurrencyStamp = Guid.NewGuid();
        session.MediaResource.ConcurrencyStamp = Guid.NewGuid();
        if (session.AttemptCount >= _settings.FinalizationMaxAttempts)
        {
            session.Status = MultipartUploadStatus.Failed;
            session.MediaResource.Status = ResourceStatus.Failed;
            session.NextAttemptAt = null;
            await MarkAudioUploadFailedAsync(
                session.MediaResource,
                cancellationToken);
        }
        else
        {
            var delayMinutes = Math.Min(
                Math.Pow(2, session.AttemptCount - 1),
                _settings.RetryMaxDelayMinutes);
            session.NextAttemptAt = _timeProvider.GetUtcNow().AddMinutes(delayMinutes);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another worker already advanced the same session.
        }
    }

    /// <summary>
    /// 分批终止并清理超过 TTL 的 Multipart Upload 会话。
    /// </summary>
    private async Task<int> CleanupExpiredMultipartAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var sessionIds = await _db.MultipartUploadSessions
            .AsNoTracking()
            .Where(session =>
                (session.Status == MultipartUploadStatus.Initiated ||
                    session.Status == MultipartUploadStatus.Aborting) &&
                session.ExpiresAt <= now &&
                (session.LeaseExpiresAt == null || session.LeaseExpiresAt <= now))
            .OrderBy(session => session.ExpiresAt)
            .ThenBy(session => session.Id)
            .Select(session => session.Id)
            .Take(_settings.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);

        var cleaned = 0;
        foreach (var sessionId in sessionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var leaseOwner = Guid.NewGuid();
            var session = await _db.MultipartUploadSessions
                .Include(value => value.MediaResource)
                .SingleOrDefaultAsync(value => value.Id == sessionId, cancellationToken);
            if (session is null ||
                session.Status is not (MultipartUploadStatus.Initiated or
                    MultipartUploadStatus.Aborting) ||
                session.LeaseExpiresAt > now)
            {
                continue;
            }
            session.Status = MultipartUploadStatus.Aborting;
            session.LeaseOwner = leaseOwner;
            session.LeaseExpiresAt = now.AddSeconds(_settings.FinalizationLeaseSeconds);
            session.ConcurrencyStamp = Guid.NewGuid();
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                continue;
            }
            try
            {
                await _objectStorage.AbortMultipartUploadAsync(
                    session.MediaResource.StagingObjectName!,
                    session.ProviderUploadId,
                    cancellationToken);
                await _objectStorage.DeleteObjectAsync(
                    session.MediaResource.StagingObjectName!,
                    cancellationToken);
                session.Status = MultipartUploadStatus.Expired;
                session.LeaseOwner = null;
                session.LeaseExpiresAt = null;
                session.MediaResource.Status = ResourceStatus.Expired;
                session.MediaResource.StagingObjectName = null;
                session.MediaResource.UploadExpiresAt = null;
                session.ConcurrencyStamp = Guid.NewGuid();
                session.MediaResource.ConcurrencyStamp = Guid.NewGuid();
                await MarkAudioUploadFailedAsync(
                    session.MediaResource,
                    cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                cleaned++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                session.LeaseOwner = null;
                session.LeaseExpiresAt = null;
                session.AttemptCount++;
                session.LastFailureCode = ErrorCodes.ObjectStorageUnavailable.ToString();
                session.ConcurrencyStamp = Guid.NewGuid();
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Expired multipart cleanup failed for session {SessionId}: {FailureType}",
                    session.Id,
                    exception.GetType().Name);
            }
        }
        return cleaned;
    }

    /// <summary>
    /// 分批删除过期简单 PUT 的 staging 对象并保留终态资源记录。
    /// </summary>
    private async Task<int> CleanupExpiredSimpleUploadsAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var resourceIds = await _db.MediaResources
            .AsNoTracking()
            .Where(resource =>
                resource.StagingObjectName != null &&
                (resource.Status == ResourceStatus.Expired ||
                    (resource.Status == ResourceStatus.Pending &&
                        resource.UploadExpiresAt <= now &&
                        resource.MultipartUploadSession == null)))
            .OrderBy(resource => resource.UploadExpiresAt)
            .ThenBy(resource => resource.Id)
            .Select(resource => resource.Id)
            .Take(_settings.CleanupBatchSize)
            .ToArrayAsync(cancellationToken);

        var cleaned = 0;
        foreach (var resourceId in resourceIds)
        {
            var resource = await _db.MediaResources.SingleOrDefaultAsync(
                value => value.Id == resourceId,
                cancellationToken);
            if (resource is null ||
                resource.Status is not (ResourceStatus.Pending or ResourceStatus.Expired) ||
                (resource.Status == ResourceStatus.Pending && resource.UploadExpiresAt > now))
            {
                continue;
            }
            if (resource.Status == ResourceStatus.Pending)
            {
                resource.Status = ResourceStatus.Expired;
                resource.ConcurrencyStamp = Guid.NewGuid();
                await MarkAudioUploadFailedAsync(resource, cancellationToken);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    continue;
                }
            }
            else if (await MarkAudioUploadFailedAsync(resource, cancellationToken))
            {
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    continue;
                }
            }
            try
            {
                var objectNames = new HashSet<string>(StringComparer.Ordinal)
                {
                    resource.StagingObjectName!,
                    resource.ObjectName
                };
                foreach (var objectName in objectNames)
                {
                    await _objectStorage.DeleteObjectAsync(
                        objectName,
                        cancellationToken);
                }
                resource.StagingObjectName = null;
                resource.UploadExpiresAt = null;
                resource.ConcurrencyStamp = Guid.NewGuid();
                await _db.SaveChangesAsync(cancellationToken);
                cleaned++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "Expired simple upload cleanup failed for resource {ResourceId}: {FailureType}",
                    resource.Id,
                    exception.GetType().Name);
            }
        }
        return cleaned;
    }

    /// <summary>
    /// 将失败或过期 Audio 源对应的上传中音频资源收敛到可重新上传的 Failed 状态。
    /// </summary>
    private async Task<bool> MarkAudioUploadFailedAsync(
        MediaResource resource,
        CancellationToken cancellationToken)
    {
        if (resource.Module != ResourceModule.Audio)
        {
            return false;
        }

        var audioResource = await _db.AudioResources.SingleOrDefaultAsync(
            value => value.SourceMediaResourceId == resource.Id &&
                value.Status == AudioResourceStatus.Uploading,
            cancellationToken);
        if (audioResource is null)
        {
            return false;
        }

        audioResource.Status = AudioResourceStatus.Failed;
        audioResource.LastFailureCode = ErrorCodes.AudioUploadIncomplete.ToString();
        audioResource.ConcurrencyStamp = Guid.NewGuid();
        return true;
    }

    /// <summary>
    /// 删除已激活资源遗留的 staging 对象，并清除会话清理标记。
    /// </summary>
    private async Task<int> CleanupActiveStagingObjectsAsync(CancellationToken cancellationToken)
    {
        var resources = await _db.MediaResources
            .Where(resource =>
                resource.Status == ResourceStatus.Active &&
                resource.StagingObjectName != null)
            .OrderBy(resource => resource.UpdatedAt)
            .ThenBy(resource => resource.Id)
            .Take(_settings.CleanupBatchSize)
            .ToListAsync(cancellationToken);
        var cleaned = 0;
        foreach (var resource in resources)
        {
            try
            {
                await _objectStorage.DeleteObjectAsync(
                    resource.StagingObjectName!,
                    cancellationToken);
                resource.StagingObjectName = null;
                resource.ConcurrencyStamp = Guid.NewGuid();
                var session = await _db.MultipartUploadSessions.SingleOrDefaultAsync(
                    value => value.MediaResourceId == resource.Id,
                    cancellationToken);
                if (session is not null)
                {
                    session.StagingCleanupRequired = false;
                    session.ConcurrencyStamp = Guid.NewGuid();
                }
                await _db.SaveChangesAsync(cancellationToken);
                cleaned++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    "Active staging cleanup failed for resource {ResourceId}: {FailureType}",
                    resource.Id,
                    exception.GetType().Name);
            }
        }
        return cleaned;
    }

    /// <summary>
    /// 验证归档对象 metadata 与资源申报值一致。
    /// </summary>
    private static void ValidateObjectMetadata(
        MediaResource resource,
        ObjectStorageMetadata metadata)
    {
        if (metadata.Size != resource.Size ||
            !string.Equals(
                metadata.ContentType?.Trim(),
                resource.ContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Stored object metadata does not match the resource.");
        }
    }
}
