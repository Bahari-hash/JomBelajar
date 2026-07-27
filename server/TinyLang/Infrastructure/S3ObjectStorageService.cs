using System.IO;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 AWS S3 client 实现对象预签名、Multipart Upload、归档和公开寻址。
/// </summary>
/// <param name="s3Client">S3-compatible 客户端。</param>
/// <param name="options">对象存储桶和 URL 配置。</param>
public sealed class S3ObjectStorageService(
    IAmazonS3 s3Client,
    IOptions<ObjectStorageSettings> options) : IObjectStorageService
{
    private readonly ObjectStorageSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task<string> PresignPutObjectAsync(
        string objectName,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.Bucket,
            Key = objectName,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.AddSeconds(_settings.PresignedUrlExpirySeconds)
        };
        request.Headers.ContentLength = size;
        return await s3Client.GetPreSignedURLAsync(request);
    }

    /// <inheritdoc />
    public string GetPublicUrl(string objectName)
    {
        var encodedObjectName = string.Join(
            '/',
            objectName.Split('/').Select(Uri.EscapeDataString));
        return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{encodedObjectName}";
    }

    /// <inheritdoc />
    public async Task<ObjectStorageMetadata?> GetObjectMetadataAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await s3Client.GetObjectMetadataAsync(
                _settings.Bucket,
                objectName,
                cancellationToken);
            return new ObjectStorageMetadata(
                response.Headers.ContentLength,
                response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateMultipartUploadAsync(
        string objectName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var response = await s3Client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest
            {
                BucketName = _settings.Bucket,
                Key = objectName,
                ContentType = contentType
            },
            cancellationToken);
        return response.UploadId;
    }

    /// <inheritdoc />
    public async Task<string> PresignUploadPartAsync(
        string objectName,
        string providerUploadId,
        int partNumber,
        long contentLength,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuredExpiry = DateTimeOffset.UtcNow.AddSeconds(
            _settings.PresignedUrlExpirySeconds);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.Bucket,
            Key = objectName,
            Verb = HttpVerb.PUT,
            UploadId = providerUploadId,
            PartNumber = partNumber,
            Expires = (configuredExpiry < expiresAt ? configuredExpiry : expiresAt).UtcDateTime
        };
        request.Headers.ContentLength = contentLength;
        return await s3Client.GetPreSignedURLAsync(request);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ObjectStorageUploadedPart>> ListUploadedPartsAsync(
        string objectName,
        string providerUploadId,
        int maxParts,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<ObjectStorageUploadedPart>();
        int? marker = null;
        do
        {
            ListPartsResponse response;
            try
            {
                response = await s3Client.ListPartsAsync(new ListPartsRequest
                {
                    BucketName = _settings.Bucket,
                    Key = objectName,
                    UploadId = providerUploadId,
                    PartNumberMarker = marker?.ToString(),
                    MaxParts = Math.Min(1000, maxParts - parts.Count)
                }, cancellationToken);
            }
            catch (AmazonS3Exception exception) when (IsUploadNotFound(exception))
            {
                throw new ObjectStorageProtocolException(
                    ObjectStorageProtocolError.UploadNotFound);
            }
            parts.AddRange(response.Parts.Select(part => new ObjectStorageUploadedPart(
                part.PartNumber.GetValueOrDefault(),
                part.ETag,
                part.Size)));
            marker = response.IsTruncated == true
                ? response.NextPartNumberMarker
                : null;
        }
        while (marker is not null && parts.Count < maxParts);

        return parts.OrderBy(part => part.PartNumber).ToArray();
    }

    /// <inheritdoc />
    public async Task CompleteMultipartUploadAsync(
        string objectName,
        string providerUploadId,
        IReadOnlyList<ObjectStorageUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.CompleteMultipartUploadAsync(
                new CompleteMultipartUploadRequest
                {
                    BucketName = _settings.Bucket,
                    Key = objectName,
                    UploadId = providerUploadId,
                    PartETags = parts.Select(part => new PartETag(
                        part.PartNumber,
                        part.ETag)).ToList()
                },
                cancellationToken);
        }
        catch (AmazonS3Exception exception) when (IsUploadNotFound(exception))
        {
            throw new ObjectStorageProtocolException(
                ObjectStorageProtocolError.UploadNotFound);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode is
            "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall")
        {
            throw new ObjectStorageProtocolException(
                ObjectStorageProtocolError.InvalidParts);
        }
    }

    /// <inheritdoc />
    public async Task AbortMultipartUploadAsync(
        string objectName,
        string providerUploadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.AbortMultipartUploadAsync(
                new AbortMultipartUploadRequest
                {
                    BucketName = _settings.Bucket,
                    Key = objectName,
                    UploadId = providerUploadId
                },
                cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // A missing upload already satisfies abort semantics.
        }
    }

    /// <inheritdoc />
    public async Task CopyObjectAsync(
        string sourceObjectName,
        string destinationObjectName,
        CancellationToken cancellationToken = default)
    {
        await s3Client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _settings.Bucket,
            SourceKey = sourceObjectName,
            DestinationBucket = _settings.Bucket,
            DestinationKey = destinationObjectName
        }, cancellationToken);

    }

    /// <inheritdoc />
    public async Task DeleteObjectAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        await s3Client.DeleteObjectAsync(_settings.Bucket, objectName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DownloadObjectAsync(
        string objectName,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        using var response = await s3Client.GetObjectAsync(
            _settings.Bucket,
            objectName,
            cancellationToken);
        await response.ResponseStream.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UploadObjectAsync(
        string objectName,
        Stream source,
        long length,
        ObjectStorageUploadOptions options,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.Bucket,
            Key = objectName,
            InputStream = source,
            AutoCloseStream = false,
            ContentType = options.ContentType,
            Headers =
            {
                ContentLength = length,
                CacheControl = options.CacheControl
            }
        };
        foreach (var (name, value) in options.Metadata ??
            new Dictionary<string, string>())
        {
            request.Metadata[name] = value;
        }
        await s3Client.PutObjectAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListObjectNamesAsync(
        string prefix,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var objectNames = new List<string>();
        string? continuationToken = null;
        do
        {
            var response = await s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _settings.Bucket,
                Prefix = prefix,
                ContinuationToken = continuationToken,
                MaxKeys = Math.Min(1000, maxCount - objectNames.Count)
            }, cancellationToken);
            objectNames.AddRange(response.S3Objects.Select(value => value.Key));
            continuationToken = response.IsTruncated == true
                ? response.NextContinuationToken
                : null;
        }
        while (continuationToken is not null && objectNames.Count < maxCount);

        return objectNames.Order(StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public async Task DeleteObjectsAsync(
        IReadOnlyCollection<string> objectNames,
        CancellationToken cancellationToken = default)
    {
        foreach (var objectName in objectNames)
        {
            await DeleteObjectAsync(objectName, cancellationToken);
        }
    }

    /// <summary>
    /// 判断 provider 异常是否表示 Multipart Upload 会话不存在。
    /// </summary>
    private static bool IsUploadNotFound(AmazonS3Exception exception)
        => exception.StatusCode == HttpStatusCode.NotFound ||
            string.Equals(exception.ErrorCode, "NoSuchUpload", StringComparison.Ordinal);
}
