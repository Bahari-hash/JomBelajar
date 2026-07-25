using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Policies;

namespace TinyLang.Services;

/// <summary>
/// 编排媒体资源记录、对象存储预签名和上传确认流程。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="objectStorage">对象存储服务。</param>
/// <param name="uploadPolicy">媒体上传限制策略。</param>
public sealed class MediaResourceService(
    IApplicationDbContext db,
    IObjectStorageService objectStorage,
    MediaUploadPolicy uploadPolicy) : IMediaResourceService
{
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

        var normalizedExtension = MediaUploadPolicy.NormalizeExtension(extension);
        var normalizedContentType = MediaUploadPolicy.NormalizeContentType(contentType);
        var objectName = $"temp/{Guid.NewGuid():N}{normalizedExtension}";

        string presignedUrl;
        try
        {
            presignedUrl = await objectStorage.PresignPutObjectAsync(
                objectName,
                normalizedContentType,
                size,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
        }

        var resource = new MediaResource
        {
            UploaderId = uploaderId,
            ObjectName = objectName,
            OriginalName = originalName,
            Module = module,
            Status = ResourceStatus.Pending,
            Size = size,
            Extension = normalizedExtension,
            ContentType = normalizedContentType,
            Url = null
        };
        db.MediaResources.Add(resource);
        await db.SaveChangesAsync(cancellationToken);

        return new MediaResourcePresignResult(resource.Id, presignedUrl, objectName);
    }

    /// <inheritdoc />
    public async Task<MediaResource> ConfirmAsync(
        Guid resourceId,
        Guid uploaderId,
        CancellationToken cancellationToken = default)
    {
        var resource = await db.MediaResources.FindAsync([resourceId], cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.MediaResourceNotFound);
        if (resource.UploaderId != uploaderId)
        {
            throw ForbiddenException.Create(ErrorCodes.MediaResourceOwnershipMismatch);
        }
        if (resource.Status == ResourceStatus.Active)
        {
            return resource;
        }
        if (resource.Status != ResourceStatus.Pending ||
            !resource.ObjectName.StartsWith("temp/", StringComparison.Ordinal))
        {
            throw ConflictException.Create(ErrorCodes.MediaResourceStatusConflict);
        }

        var finalObjectName = CreateFinalObjectName(resource);
        var objectMetadata = await GetObjectMetadataAsync(resource.ObjectName, cancellationToken);
        if (objectMetadata is not null)
        {
            ValidateObjectMetadata(resource, objectMetadata);
            try
            {
                await objectStorage.MoveObjectAsync(
                    resource.ObjectName,
                    finalObjectName,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var recoveredMetadata = await GetObjectMetadataAsync(
                    finalObjectName,
                    cancellationToken);
                if (recoveredMetadata is null)
                {
                    throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
                }
                ValidateObjectMetadata(resource, recoveredMetadata);
            }
        }
        else
        {
            var recoveredMetadata = await GetObjectMetadataAsync(finalObjectName, cancellationToken);
            if (recoveredMetadata is null)
            {
                throw ConflictException.Create(ErrorCodes.MediaResourceUploadIncomplete);
            }
            ValidateObjectMetadata(resource, recoveredMetadata);
        }

        resource.ObjectName = finalObjectName;
        resource.Url = objectStorage.GetPublicUrl(finalObjectName);
        resource.Status = ResourceStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return resource;
    }

    /// <summary>
    /// 读取对象元数据，并将非取消类存储异常映射为稳定业务错误。
    /// </summary>
    /// <param name="objectName">对象名称。</param>
    /// <param name="cancellationToken">用于取消存储请求的令牌。</param>
    /// <returns>对象元数据；对象不存在时为 <see langword="null"/>。</returns>
    private async Task<ObjectStorageMetadata?> GetObjectMetadataAsync(
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await objectStorage.GetObjectMetadataAsync(objectName, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw UnexpectedException.Create(ErrorCodes.ObjectStorageUnavailable);
        }
    }

    /// <summary>
    /// 校验实际对象大小和媒体类型是否与资源申报值一致。
    /// </summary>
    /// <param name="resource">待确认资源记录。</param>
    /// <param name="objectMetadata">对象存储返回的元数据。</param>
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
    /// 按上传策略校验模块、文件名、扩展名、大小和媒体类型。
    /// </summary>
    /// <param name="originalName">客户端原始文件名。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="size">文件大小，单位为字节。</param>
    /// <param name="contentType">文件媒体类型。</param>
    /// <param name="module">资源所属业务模块。</param>
    private void ValidateUploadInput(
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module)
    {
        if (!uploadPolicy.IsSupportedModule(module))
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
        if (extension.Length > 16 || !uploadPolicy.IsExtensionAllowed(module, extension))
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
        if (!uploadPolicy.IsSizeAllowed(module, size))
        {
            throw new RequestValidationException(ErrorCodes.MediaSizeLimitExceeded);
        }
        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new RequestValidationException(ErrorCodes.MediaContentTypeRequired);
        }
        if (contentType.Length > 100 ||
            !uploadPolicy.IsContentTypeAllowed(module, extension, contentType))
        {
            throw new RequestValidationException(ErrorCodes.MediaContentTypeInvalid);
        }
    }

    /// <summary>
    /// 根据资源模块、创建月份和资源标识构建最终对象名称。
    /// </summary>
    /// <param name="resource">待激活媒体资源。</param>
    /// <returns>模块目录下的最终对象名称。</returns>
    /// <exception cref="ArgumentOutOfRangeException">资源模块不受支持。</exception>
    private static string CreateFinalObjectName(MediaResource resource)
    {
        var modulePath = resource.Module switch
        {
            ResourceModule.Avatar => "avatars",
            ResourceModule.ArticlePicture => "article_pictures",
            ResourceModule.Audio => "audios",
            ResourceModule.CourseVideo => "courses",
            _ => throw new ArgumentOutOfRangeException(
                nameof(resource.Module),
                resource.Module,
                "Unsupported resource module.")
        };
        var createdAt = resource.CreatedAt == default
            ? DateTimeOffset.UtcNow
            : resource.CreatedAt;
        return $"{modulePath}/{createdAt:yyyy/MM}/{resource.Id:N}{resource.Extension}";
    }
}
