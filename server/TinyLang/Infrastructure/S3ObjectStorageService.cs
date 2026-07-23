using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System.Net;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

public sealed class S3ObjectStorageService(
    IAmazonS3 s3Client,
    IOptions<ObjectStorageSettings> options) : IObjectStorageService
{
    private readonly ObjectStorageSettings _settings = options.Value;

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

    public string GetPublicUrl(string objectName)
    {
        var encodedObjectName = string.Join(
            '/',
            objectName.Split('/').Select(Uri.EscapeDataString));
        return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{encodedObjectName}";
    }

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
