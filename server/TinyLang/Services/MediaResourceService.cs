using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Policies;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 编排简单 PUT、Multipart Upload、资源确认及其可恢复状态转换。
/// </summary>
public sealed class MediaResourceService : IMediaResourceService
{
    private readonly ILogger<MediaResourceService> _logger;
    private readonly IApplicationDbContext _db;
    private readonly IObjectStorageService _objectStorage;
    private readonly MediaUploadPolicy _uploadPolicy;
    private readonly MultipartUploadSettings _multipartSettings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 使用数据库、对象存储和上传策略创建媒体资源服务。
    /// </summary>
    /// <param name="logger">日志打印服务。</param>
    /// <param name="db">应用数据库上下文。</param>
    /// <param name="objectStorage">对象存储服务。</param>
    /// <param name="uploadPolicy">媒体上传限制策略。</param>
    /// <param name="multipartOptions">Multipart Upload 和配额配置。</param>
    /// <param name="timeProvider">提供可测试 UTC 时间的时钟。</param>
    public MediaResourceService(
        ILogger<MediaResourceService> logger,
        IApplicationDbContext db,
        IObjectStorageService objectStorage,
        MediaUploadPolicy uploadPolicy,
        IOptions<MultipartUploadSettings> multipartOptions,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _db = db;
        _objectStorage = objectStorage;
        _uploadPolicy = uploadPolicy;
        _multipartSettings = multipartOptions.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<MediaResourcePresignResult> CreatePendingResourceAndPresignAsync(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadInput(originalName, extension, size, contentType, module);
        if (RequiresMultipart(size))
        {
            throw new RequestValidationException(ErrorCodes.MultipartUploadRequired);
        }

        var now = _timeProvider.GetUtcNow();
        var normalizedExtension = MediaUploadPolicy.NormalizeExtension(extension);
        var normalizedContentType = MediaUploadPolicy.NormalizeContentType(contentType);
        var resource = CreatePendingResource(
            uploaderId,
            originalName,
            normalizedExtension,
            size,
            normalizedContentType,
            module,
            now);
        _db.MediaResources.Add(resource);
        await _db.SaveChangesAsync(cancellationToken);

        string presignedUrl;
        try
        {
            presignedUrl = await _objectStorage.PresignPutObjectAsync(
                resource.StagingObjectName!,
                normalizedContentType,
                size,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("OSS存储服务故障: {Exception}", exception);
            throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
        }

        return new MediaResourcePresignResult(
            resource.Id,
            presignedUrl,
            resource.StagingObjectName!);
    }

    /// <inheritdoc />
    public async Task<MultipartUploadCreateResult> CreateMultipartUploadAsync(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        CancellationToken cancellationToken = default)
    {
        ValidateUploadInput(originalName, extension, size, contentType, module);
        if (!RequiresMultipart(size))
        {
            throw new RequestValidationException(ErrorCodes.MultipartUploadNotRequired);
        }

        var partSize = Megabytes(_multipartSettings.PartSizeMB);
        var partCount = checked((int)((size + partSize - 1) / partSize));
        if (partCount > _multipartSettings.MaxPartCount)
        {
            throw new RequestValidationException(ErrorCodes.MediaSizeLimitExceeded);
        }

        await EnsureIncompleteUploadQuotaAsync(uploaderId, size, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var normalizedExtension = MediaUploadPolicy.NormalizeExtension(extension);
        var normalizedContentType = MediaUploadPolicy.NormalizeContentType(contentType);
        var resource = CreatePendingResource(
            uploaderId,
            originalName,
            normalizedExtension,
            size,
            normalizedContentType,
            module,
            now);

        string providerUploadId;
        try
        {
            providerUploadId = await _objectStorage.CreateMultipartUploadAsync(
                resource.StagingObjectName!,
                normalizedContentType,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("OSS存储服务故障: {Exception}", exception);
            throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
        }

        var session = new MultipartUploadSession
        {
            MediaResource = resource,
            MediaResourceId = resource.Id,
            UploaderId = uploaderId,
            ProviderUploadId = providerUploadId,
            PartSize = partSize,
            PartCount = partCount,
            ExpiresAt = resource.UploadExpiresAt!.Value
        };
        _db.MediaResources.Add(resource);
        _db.MultipartUploadSessions.Add(session);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await TryAbortOrphanedMultipartAsync(
                resource.StagingObjectName!,
                providerUploadId,
                cancellationToken);
            throw;
        }

        return new MultipartUploadCreateResult(
            resource.Id,
            session.Id,
            session.PartSize,
            session.PartCount,
            session.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MultipartPartPresignResult>> PresignMultipartPartsAsync(
        Guid sessionId,
        Guid uploaderId,
        IReadOnlyCollection<int> partNumbers,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, cancellationToken);
        EnsureSessionOwner(session, uploaderId);
        EnsureSessionUsable(session);
        ValidatePartNumbers(session, partNumbers);

        var results = new List<MultipartPartPresignResult>(partNumbers.Count);
        foreach (var partNumber in partNumbers.Order())
        {
            var contentLength = GetPartSize(session.MediaResource.Size, session.PartSize, partNumber);
            try
            {
                var url = await _objectStorage.PresignUploadPartAsync(
                    session.MediaResource.StagingObjectName!,
                    session.ProviderUploadId,
                    partNumber,
                    contentLength,
                    session.ExpiresAt,
                    cancellationToken);
                results.Add(new MultipartPartPresignResult(
                    partNumber,
                    url,
                    contentLength,
                    session.ExpiresAt));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError("OSS存储服务故障: {Exception}", exception);
                throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<MultipartUploadStatusResult> GetMultipartUploadAsync(
        Guid sessionId,
        Guid uploaderId,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, cancellationToken);
        EnsureSessionOwner(session, uploaderId);

        IReadOnlyList<ObjectStorageUploadedPart> uploadedParts = [];
        if (session.Status == MultipartUploadStatus.Initiated &&
            _timeProvider.GetUtcNow() < session.ExpiresAt)
        {
            try
            {
                uploadedParts = await _objectStorage.ListUploadedPartsAsync(
                    session.MediaResource.StagingObjectName!,
                    session.ProviderUploadId,
                    session.PartCount,
                    cancellationToken);
            }
            catch (ObjectStorageProtocolException exception) when (
                exception.Reason == ObjectStorageProtocolError.UploadNotFound)
            {
                throw ConflictException.Create(ErrorCodes.MediaResourceUploadIncomplete);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError("OSS存储服务故障: {Exception}", exception);
                throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
            }
        }

        return ToStatusResult(session, uploadedParts);
    }

    /// <inheritdoc />
    public async Task<MultipartUploadStatusResult> CompleteMultipartUploadAsync(
        Guid sessionId,
        Guid uploaderId,
        IReadOnlyCollection<ObjectStorageUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, cancellationToken);
        EnsureSessionOwner(session, uploaderId);
        if (session.Status is MultipartUploadStatus.Finalizing or
            MultipartUploadStatus.Completed)
        {
            return ToStatusResult(session, []);
        }
        if (session.Status is not (MultipartUploadStatus.Initiated or
            MultipartUploadStatus.Completing))
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceStatusConflict);
        }
        EnsureNotExpired(session);

        var orderedParts = ValidateCompletionParts(session, parts);
        if (session.Status == MultipartUploadStatus.Initiated)
        {
            session.Status = MultipartUploadStatus.Completing;
            session.MediaResource.Status = ResourceStatus.Finalizing;
            TouchConcurrency(session);
            await SaveWithConcurrencyMappingAsync(cancellationToken);
        }

        var metadata = await GetObjectMetadataAsync(
            session.MediaResource.StagingObjectName!,
            cancellationToken);
        if (metadata is null)
        {
            try
            {
                await _objectStorage.CompleteMultipartUploadAsync(
                    session.MediaResource.StagingObjectName!,
                    session.ProviderUploadId,
                    orderedParts,
                    cancellationToken);
            }
            catch (ObjectStorageProtocolException exception) when (
                exception.Reason == ObjectStorageProtocolError.InvalidParts)
            {
                throw new RequestValidationException(ErrorCodes.MultipartUploadPartsInvalid);
            }
            catch (ObjectStorageProtocolException exception) when (
                exception.Reason == ObjectStorageProtocolError.UploadNotFound)
            {
                metadata = await GetObjectMetadataAsync(
                    session.MediaResource.StagingObjectName!,
                    cancellationToken);
                if (metadata is null)
                {
                    throw ConflictException.Create(ErrorCodes.MediaResourceUploadIncomplete);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                metadata = await GetObjectMetadataAsync(
                    session.MediaResource.StagingObjectName!,
                    cancellationToken);
                if (metadata is null)
                {
                    _logger.LogError("OSS存储服务故障: {Exception}", exception);
                    throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
                }
            }
        }

        metadata ??= await GetObjectMetadataAsync(
            session.MediaResource.StagingObjectName!,
            cancellationToken);
        if (metadata is null)
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceUploadIncomplete);
        }
        ValidateObjectMetadata(session.MediaResource, metadata);

        session.Status = MultipartUploadStatus.Finalizing;
        session.NextAttemptAt = _timeProvider.GetUtcNow();
        session.MediaResource.Status = ResourceStatus.Finalizing;
        TouchConcurrency(session);
        await SaveWithConcurrencyMappingAsync(cancellationToken);
        return ToStatusResult(session, []);
    }

    /// <inheritdoc />
    public async Task AbortMultipartUploadAsync(
        Guid sessionId,
        Guid uploaderId,
        CancellationToken cancellationToken = default)
    {
        var session = await FindSessionAsync(sessionId, cancellationToken);
        EnsureSessionOwner(session, uploaderId);
        if (session.Status is MultipartUploadStatus.Aborted or MultipartUploadStatus.Expired)
        {
            return;
        }
        if (session.Status is not (MultipartUploadStatus.Initiated or MultipartUploadStatus.Aborting))
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceStatusConflict);
        }

        if (session.Status == MultipartUploadStatus.Initiated)
        {
            session.Status = MultipartUploadStatus.Aborting;
            TouchConcurrency(session);
            await SaveWithConcurrencyMappingAsync(cancellationToken);
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
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("OSS存储服务故障: {Exception}", exception);
            throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
        }

        session.Status = MultipartUploadStatus.Aborted;
        session.MediaResource.Status = ResourceStatus.Aborted;
        session.MediaResource.StagingObjectName = null;
        session.MediaResource.UploadExpiresAt = null;
        TouchConcurrency(session);
        await SaveWithConcurrencyMappingAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MediaResource> ConfirmAsync(
        Guid resourceId,
        Guid uploaderId,
        CancellationToken cancellationToken = default)
    {
        var resource = await _db.MediaResources.FindAsync([resourceId], cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.MediaResourceNotFound);
        if (resource.UploaderId != uploaderId)
        {
            throw ForbiddenException.Create(ErrorCodes.MediaResourceOwnershipMismatch);
        }
        if (resource.Status == ResourceStatus.Active)
        {
            return resource;
        }
        if (resource.Status == ResourceStatus.Finalizing)
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceFinalizing);
        }
        if (resource.Status != ResourceStatus.Pending)
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceStatusConflict);
        }
        if (resource.StagingObjectName is not null && RequiresMultipart(resource.Size))
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceUploadIncomplete);
        }
        if (resource.UploadExpiresAt is { } expiresAt && _timeProvider.GetUtcNow() >= expiresAt)
        {
            throw ConflictException.Create(ErrorCodes.MultipartUploadExpired);
        }

        var isLegacy = resource.StagingObjectName is null &&
            resource.ObjectName.StartsWith("temp/", StringComparison.Ordinal);
        var stagingObjectName = resource.StagingObjectName ?? resource.ObjectName;
        var finalObjectName = isLegacy
            ? CreateFinalObjectName(
                resource.Module,
                resource.Id,
                resource.Extension,
                resource.CreatedAt == default ? _timeProvider.GetUtcNow() : resource.CreatedAt)
            : resource.ObjectName;

        var stagingDeleted = await ArchiveAndValidateAsync(
            resource,
            stagingObjectName,
            finalObjectName,
            cancellationToken);

        resource.ObjectName = finalObjectName;
        resource.StagingObjectName = stagingDeleted ? null : stagingObjectName;
        resource.Url = _objectStorage.GetPublicUrl(finalObjectName);
        resource.Status = ResourceStatus.Active;
        resource.UploadExpiresAt = null;
        resource.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithConcurrencyMappingAsync(cancellationToken);
        return resource;
    }

    /// <summary>
    /// 归档简单上传对象，并从 staging 或已存在的最终对象恢复重试。
    /// </summary>
    private async Task<bool> ArchiveAndValidateAsync(
        MediaResource resource,
        string stagingObjectName,
        string finalObjectName,
        CancellationToken cancellationToken)
    {
        var stagingMetadata = await GetObjectMetadataAsync(stagingObjectName, cancellationToken);
        if (stagingMetadata is not null)
        {
            ValidateObjectMetadata(resource, stagingMetadata);
            try
            {
                await _objectStorage.CopyObjectAsync(
                    stagingObjectName,
                    finalObjectName,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var recovered = await GetObjectMetadataAsync(finalObjectName, cancellationToken);
                if (recovered is null)
                {
                    throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
                }
                ValidateObjectMetadata(resource, recovered);
            }

            try
            {
                await _objectStorage.DeleteObjectAsync(stagingObjectName, cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return false;
            }
        }
        else
        {
            var finalMetadata = await GetObjectMetadataAsync(finalObjectName, cancellationToken);
            if (finalMetadata is null)
            {
                throw ConflictException.Create(ErrorCodes.MediaResourceUploadIncomplete);
            }
            ValidateObjectMetadata(resource, finalMetadata);
            return true;
        }
    }

    /// <summary>
    /// 创建具有不可变最终 key 和独立 staging key 的待上传资源。
    /// </summary>
    private MediaResource CreatePendingResource(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        DateTimeOffset now)
    {
        var resource = new MediaResource
        {
            UploaderId = uploaderId,
            ObjectName = string.Empty,
            OriginalName = originalName,
            Module = module,
            Status = ResourceStatus.Pending,
            Size = size,
            Extension = extension,
            ContentType = contentType,
            Url = null,
            UploadExpiresAt = now.AddMinutes(_multipartSettings.SessionTtlMinutes)
        };
        resource.ObjectName = CreateFinalObjectName(
            module,
            resource.Id,
            extension,
            now);
        resource.StagingObjectName = $"staging/{resource.Id:N}/{Guid.NewGuid():N}{extension}";
        return resource;
    }

    /// <summary>
    /// 加载包含媒体资源的 Multipart Upload 会话。
    /// </summary>
    private async Task<MultipartUploadSession> FindSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
        => await _db.MultipartUploadSessions
            .Include(x => x.MediaResource)
            .SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.MultipartUploadNotFound);

    /// <summary>
    /// 验证当前用户拥有指定上传会话。
    /// </summary>
    private static void EnsureSessionOwner(MultipartUploadSession session, Guid uploaderId)
    {
        if (session.UploaderId != uploaderId)
        {
            throw ForbiddenException.Create(ErrorCodes.MultipartUploadOwnershipMismatch);
        }
    }

    /// <summary>
    /// 验证会话仍允许签发或列举 parts。
    /// </summary>
    private void EnsureSessionUsable(MultipartUploadSession session)
    {
        if (session.Status != MultipartUploadStatus.Initiated)
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceStatusConflict);
        }
        EnsureNotExpired(session);
    }

    /// <summary>
    /// 验证上传会话未超过服务端保存的失效时间。
    /// </summary>
    private void EnsureNotExpired(MultipartUploadSession session)
    {
        if (_timeProvider.GetUtcNow() >= session.ExpiresAt)
        {
            throw ConflictException.Create(ErrorCodes.MultipartUploadExpired);
        }
    }

    /// <summary>
    /// 校验批量签名 part 编号的数量、唯一性和范围。
    /// </summary>
    private void ValidatePartNumbers(
        MultipartUploadSession session,
        IReadOnlyCollection<int> partNumbers)
    {
        if (partNumbers.Count == 0 ||
            partNumbers.Count > _multipartSettings.PartPresignBatchLimit ||
            partNumbers.Distinct().Count() != partNumbers.Count ||
            partNumbers.Any(number => number < 1 || number > session.PartCount))
        {
            throw new RequestValidationException(ErrorCodes.MultipartUploadPartsInvalid);
        }
    }

    /// <summary>
    /// 校验完成请求恰好覆盖全部 parts，并按编号排序。
    /// </summary>
    private static IReadOnlyList<ObjectStorageUploadedPart> ValidateCompletionParts(
        MultipartUploadSession session,
        IReadOnlyCollection<ObjectStorageUploadedPart> parts)
    {
        if (parts.Count != session.PartCount ||
            parts.Select(part => part.PartNumber).Distinct().Count() != parts.Count ||
            parts.Any(part =>
                part.PartNumber < 1 ||
                part.PartNumber > session.PartCount ||
                string.IsNullOrWhiteSpace(part.ETag) ||
                part.ETag.Length > 256 ||
                part.ETag.Any(char.IsControl)))
        {
            throw new RequestValidationException(ErrorCodes.MultipartUploadPartsInvalid);
        }

        var ordered = parts.OrderBy(part => part.PartNumber).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index].PartNumber != index + 1)
            {
                throw new RequestValidationException(ErrorCodes.MultipartUploadPartsInvalid);
            }
        }
        return ordered;
    }

    /// <summary>
    /// 确保用户未完成上传数量和声明容量未超过配置上限。
    /// </summary>
    private async Task EnsureIncompleteUploadQuotaAsync(
        Guid uploaderId,
        long requestedSize,
        CancellationToken cancellationToken)
    {
        var incomplete = _db.MediaResources.AsNoTracking().Where(resource =>
            resource.UploaderId == uploaderId &&
            (resource.Status == ResourceStatus.Pending ||
                resource.Status == ResourceStatus.Finalizing));
        var count = await incomplete.CountAsync(cancellationToken);
        var bytes = await incomplete.SumAsync(resource => (long?)resource.Size, cancellationToken) ?? 0;
        if (count >= _multipartSettings.MaxIncompleteUploadCountPerUser ||
            bytes + requestedSize > Megabytes(_multipartSettings.MaxIncompleteUploadMBPerUser))
        {
            throw ConflictException.Create(ErrorCodes.MultipartUploadQuotaExceeded);
        }
    }

    /// <summary>
    /// 读取对象 metadata，并将存储异常映射为稳定业务错误。
    /// </summary>
    private async Task<ObjectStorageMetadata?> GetObjectMetadataAsync(
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _objectStorage.GetObjectMetadataAsync(objectName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("OSS存储服务故障: {Exception}", exception);
            throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
        }
    }

    /// <summary>
    /// 尽力终止数据库持久化失败后遗留的 provider 会话。
    /// </summary>
    private async Task TryAbortOrphanedMultipartAsync(
        string objectName,
        string providerUploadId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _objectStorage.AbortMultipartUploadAsync(
                objectName,
                providerUploadId,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Bucket lifecycle remains the final recovery path for an untracked upload.
        }
    }

    /// <summary>
    /// 保存状态转换，并将数据库并发竞争映射为稳定冲突。
    /// </summary>
    private async Task SaveWithConcurrencyMappingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceStatusConflict);
        }
    }

    /// <summary>
    /// 同步更新会话和资源的应用并发 token。
    /// </summary>
    private static void TouchConcurrency(MultipartUploadSession session)
    {
        session.ConcurrencyStamp = Guid.NewGuid();
        session.MediaResource.ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>
    /// 校验对象实际大小和 Content-Type 与资源申报元数据一致。
    /// </summary>
    private static void ValidateObjectMetadata(
        MediaResource resource,
        ObjectStorageMetadata objectMetadata)
    {
        if (objectMetadata.Size != resource.Size)
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceSizeMismatch);
        }
        if (!string.Equals(
            objectMetadata.ContentType?.Trim(),
            resource.ContentType,
            StringComparison.OrdinalIgnoreCase))
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceContentTypeMismatch);
        }
    }

    /// <summary>
    /// 按上传策略校验模块、文件名、扩展名、大小和 Content-Type。
    /// </summary>
    private void ValidateUploadInput(
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module)
    {
        if (!_uploadPolicy.IsSupportedModule(module))
        {
            throw new RequestValidationException(ErrorCodes.ResourceModuleInvalid);
        }
        if (string.IsNullOrWhiteSpace(originalName))
        {
            throw new RequestValidationException(ErrorCodes.MediaOriginalNameRequired);
        }
        if (originalName.Length > 255)
        {
            throw new RequestValidationException(ErrorCodes.MediaOriginalNameLengthLimit);
        }
        if (!MediaUploadPolicy.IsSafeFileName(originalName))
        {
            throw new RequestValidationException(ErrorCodes.MediaOriginalNameInvalid);
        }
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new RequestValidationException(ErrorCodes.MediaExtensionRequired);
        }
        if (extension.Length > 16 || !_uploadPolicy.IsExtensionAllowed(module, extension))
        {
            throw new RequestValidationException(ErrorCodes.MediaExtensionInvalid);
        }
        if (!MediaUploadPolicy.ExtensionMatchesName(originalName, extension))
        {
            throw new RequestValidationException(ErrorCodes.MediaExtensionMismatch);
        }
        if (size <= 0)
        {
            throw new RequestValidationException(ErrorCodes.MediaSizeInvalid);
        }
        if (!_uploadPolicy.IsSizeAllowed(module, size))
        {
            throw new RequestValidationException(ErrorCodes.MediaSizeLimitExceeded);
        }
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new RequestValidationException(ErrorCodes.MediaContentTypeRequired);
        }
        if (contentType.Length > 100 ||
            !_uploadPolicy.IsContentTypeAllowed(module, extension, contentType))
        {
            throw new RequestValidationException(ErrorCodes.MediaContentTypeInvalid);
        }
    }

    /// <summary>
    /// 根据配置判断文件是否必须走 Multipart Upload。
    /// </summary>
    private bool RequiresMultipart(long size)
        => size >= Megabytes(_multipartSettings.ThresholdMB);

    /// <summary>
    /// 计算指定 part 的精确字节数，末片使用剩余大小。
    /// </summary>
    private static long GetPartSize(long totalSize, long partSize, int partNumber)
        => Math.Min(partSize, totalSize - ((long)(partNumber - 1) * partSize));

    /// <summary>
    /// 将会话实体映射为不包含 provider 内部标识的应用结果。
    /// </summary>
    private static MultipartUploadStatusResult ToStatusResult(
        MultipartUploadSession session,
        IReadOnlyList<ObjectStorageUploadedPart> uploadedParts)
        => new(
            session.MediaResourceId,
            session.Id,
            session.Status,
            session.PartSize,
            session.PartCount,
            session.ExpiresAt,
            uploadedParts);

    /// <summary>
    /// 根据模块、资源标识和创建月份构建不可变最终对象名称。
    /// </summary>
    private static string CreateFinalObjectName(
        ResourceModule module,
        Guid resourceId,
        string extension,
        DateTimeOffset createdAt)
    {
        var modulePath = module switch
        {
            ResourceModule.Avatar => "avatars",
            ResourceModule.ArticlePicture => "article_pictures",
            ResourceModule.VideoCover => "video_covers",
            ResourceModule.Audio => "audios",
            ResourceModule.CourseVideo => "courses",
            _ => throw new ArgumentOutOfRangeException(
                nameof(module),
                module,
                "Unsupported resource module.")
        };
        return $"{modulePath}/{createdAt:yyyy/MM}/{resourceId:N}{extension}";
    }

    /// <summary>
    /// 将 MiB 配置转换为字节数。
    /// </summary>
    private static long Megabytes(int value) => (long)value * 1024 * 1024;
}
