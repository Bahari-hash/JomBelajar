using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using TinyLang.Infrastructure;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.IntegrationTests;

/// <summary>
/// 在显式提供的真实 MinIO 环境中验证标准 S3 Multipart Upload 兼容性。
/// </summary>
public sealed class MinioMultipartUploadTests
{
    /// <summary>
    /// 通过服务生成的预签名 URL 上传两个 parts，列举、完成并校验最终 metadata。
    /// </summary>
    [Fact]
    public async Task PresignedMultipartFlowShouldWorkAgainstConfiguredMinio()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("TINYLANG_MINIO_SERVICE_URL");
        var accessKey = Environment.GetEnvironmentVariable("TINYLANG_MINIO_ACCESS_KEY");
        var secretKey = Environment.GetEnvironmentVariable("TINYLANG_MINIO_SECRET_KEY");
        var bucket = Environment.GetEnvironmentVariable("TINYLANG_MINIO_BUCKET");
        if (new[] { serviceUrl, accessKey, secretKey, bucket }
            .Any(string.IsNullOrWhiteSpace))
        {
            Assert.Skip(
                "Set TINYLANG_MINIO_SERVICE_URL, TINYLANG_MINIO_ACCESS_KEY, " +
                "TINYLANG_MINIO_SECRET_KEY and TINYLANG_MINIO_BUCKET to run this test.");
        }

        using var s3Client = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true
            });
        var storage = new S3ObjectStorageService(
            s3Client,
            Options.Create(new ObjectStorageSettings
            {
                ServiceUrl = serviceUrl,
                Region = "us-east-1",
                AccessKey = accessKey,
                SecretKey = secretKey,
                Bucket = bucket!,
                ForcePathStyle = true,
                PublicBaseUrl = $"{serviceUrl!.TrimEnd('/')}/{bucket}",
                PresignedUrlExpirySeconds = 900
            }));
        var objectName = $"staging/integration/{Guid.NewGuid():N}.bin";
        string? uploadId = null;
        var completed = false;
        try
        {
            uploadId = await storage.CreateMultipartUploadAsync(
                objectName,
                "application/octet-stream",
                TestContext.Current.CancellationToken);
            using var httpClient = new HttpClient();
            var etags = new List<ObjectStorageUploadedPart>();
            foreach (var (partNumber, size) in new[]
            {
                (1, 5 * 1024 * 1024),
                (2, 1024 * 1024)
            })
            {
                var url = await storage.PresignUploadPartAsync(
                    objectName,
                    uploadId,
                    partNumber,
                    size,
                    DateTimeOffset.UtcNow.AddMinutes(15),
                    TestContext.Current.CancellationToken);
                using var content = new ByteArrayContent(new byte[size]);
                content.Headers.ContentLength = size;
                using var response = await httpClient.PutAsync(
                    url,
                    content,
                    TestContext.Current.CancellationToken);
                response.EnsureSuccessStatusCode();
                var etag = response.Headers.ETag?.Tag
                    ?? throw new InvalidOperationException("MinIO did not return a part ETag.");
                etags.Add(new ObjectStorageUploadedPart(partNumber, etag, size));
            }

            var listed = await storage.ListUploadedPartsAsync(
                objectName,
                uploadId,
                10,
                TestContext.Current.CancellationToken);
            Assert.Equal([1, 2], listed.Select(part => part.PartNumber));

            await storage.CompleteMultipartUploadAsync(
                objectName,
                uploadId,
                etags,
                TestContext.Current.CancellationToken);
            completed = true;
            var metadata = await storage.GetObjectMetadataAsync(
                objectName,
                TestContext.Current.CancellationToken);
            Assert.NotNull(metadata);
            Assert.Equal(6L * 1024 * 1024, metadata.Size);
            Assert.Equal("application/octet-stream", metadata.ContentType);
        }
        finally
        {
            if (!completed && uploadId is not null)
            {
                await storage.AbortMultipartUploadAsync(
                    objectName,
                    uploadId,
                    TestContext.Current.CancellationToken);
            }
            await storage.DeleteObjectAsync(
                objectName,
                TestContext.Current.CancellationToken);
        }
    }
}
