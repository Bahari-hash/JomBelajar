using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System.Net;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 AWS S3 client 实现对象预签名、元数据查询、移动和公开寻址。
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
    public async Task MoveObjectAsync(
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

        await s3Client.DeleteObjectAsync(
            _settings.Bucket,
            sourceObjectName,
            cancellationToken);
    }
}
