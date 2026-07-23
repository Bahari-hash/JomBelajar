using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Policies;

namespace TinyLang.Services;

public sealed class MediaResourceService(
    IApplicationDbContext db,
    IObjectStorageService objectStorage,
    MediaUploadPolicy uploadPolicy) : IMediaResourceService
{
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
