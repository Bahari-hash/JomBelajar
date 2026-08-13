using System.IO;
using System.Linq;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证 S3-compatible 对象存储请求映射和稳定错误分类。
/// </summary>
public sealed class S3ObjectStorageServiceTests
{
    [Fact]
    public async Task DerivedUploadShouldMapLengthContentTypeAndCacheControl()
    {
        PutObjectRequest? captured = null;
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(value => value.PutObjectAsync(
                It.IsAny<PutObjectRequest>(),
                TestContext.Current.CancellationToken))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PutObjectResponse());
        var service = CreateService(s3Client.Object);
        await using var content = new MemoryStream(new byte[128]);

        await service.UploadObjectAsync(
            "videos/id/outputs/version/master.m3u8",
            content,
            content.Length,
            new ObjectStorageUploadOptions(
                "application/vnd.apple.mpegurl",
                "public,max-age=31536000,immutable"),
            TestContext.Current.CancellationToken);

        captured.Should().NotBeNull();
        captured!.Key.Should().Be("videos/id/outputs/version/master.m3u8");
        captured.ContentType.Should().Be("application/vnd.apple.mpegurl");
        captured.Headers.ContentLength.Should().Be(128);
        captured.Headers.CacheControl.Should().Be("public,max-age=31536000,immutable");
        captured.InputStream.Should().BeSameAs(content);
        captured.AutoCloseStream.Should().BeFalse();
    }

    [Fact]
    public async Task PrefixListingShouldReturnSortedObjectNames()
    {
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(value => value.ListObjectsV2Async(
                It.IsAny<ListObjectsV2Request>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(new ListObjectsV2Response
            {
                S3Objects =
                [
                    new S3Object { Key = "videos/id/version/z.ts" },
                    new S3Object { Key = "videos/id/version/a.ts" }
                ],
                IsTruncated = false
            });
        var service = CreateService(s3Client.Object);

        var result = await service.ListObjectNamesAsync(
            "videos/id/version/",
            10,
            TestContext.Current.CancellationToken);

        result.Should().Equal(
            "videos/id/version/a.ts",
            "videos/id/version/z.ts");
    }

    [Fact]
    public async Task PresignedUrlShouldSignContentLengthAndContentTypeHeaders()
    {
        using var s3Client = new AmazonS3Client(
            new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonS3Config
            {
                ServiceURL = "http://localhost:9000",
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true
            });
        var service = CreateService(s3Client);

        var url = await service.PresignPutObjectAsync(
            "temp/image.png",
            "image/png",
            1024,
            TestContext.Current.CancellationToken);
        var decodedUrl = Uri.UnescapeDataString(url);

        decodedUrl.Should().Contain("content-length");
        decodedUrl.Should().Contain("content-type");
    }

    [Fact]
    public async Task ShouldLockContentTypeInPutPresignedUrl()
    {
        GetPreSignedUrlRequest? capturedRequest = null;
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => capturedRequest = request)
            .ReturnsAsync("https://s3.example.com/presigned");
        var service = CreateService(s3Client.Object);

        var result = await service.PresignPutObjectAsync(
            "temp/image.png",
            "image/png",
            1024,
            TestContext.Current.CancellationToken);

        result.Should().Be("https://s3.example.com/presigned");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.BucketName.Should().Be("tiny-lang-media");
        capturedRequest.Key.Should().Be("temp/image.png");
        capturedRequest.Verb.Should().Be(HttpVerb.PUT);
        capturedRequest.ContentType.Should().Be("image/png");
        capturedRequest.Headers.ContentLength.Should().Be(1024);
    }

    [Fact]
    public async Task ShouldReadObjectMetadata()
    {
        var response = new GetObjectMetadataResponse();
        response.Headers.ContentLength = 1024;
        response.Headers.ContentType = "image/png";
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.GetObjectMetadataAsync(
                "tiny-lang-media",
                "temp/image.png",
                TestContext.Current.CancellationToken))
            .ReturnsAsync(response);
        var service = CreateService(s3Client.Object);

        var metadata = await service.GetObjectMetadataAsync(
            "temp/image.png",
            TestContext.Current.CancellationToken);

        metadata.Should().NotBeNull();
        metadata!.Size.Should().Be(1024);
        metadata.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task ShouldMapCopyObjectRequestWithoutDeletingSource()
    {
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.CopyObjectAsync(
                It.IsAny<CopyObjectRequest>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(new CopyObjectResponse());
        var service = CreateService(s3Client.Object);

        await service.CopyObjectAsync(
            "temp/image.png",
            "avatars/2026/07/image.png",
            TestContext.Current.CancellationToken);

        s3Client.Verify(x => x.CopyObjectAsync(
            It.Is<CopyObjectRequest>(request =>
                request.SourceBucket == "tiny-lang-media" &&
                request.SourceKey == "temp/image.png" &&
                request.DestinationBucket == "tiny-lang-media" &&
                request.DestinationKey == "avatars/2026/07/image.png"),
            TestContext.Current.CancellationToken), Times.Once);
        s3Client.Verify(x => x.DeleteObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldDeleteObjectFromConfiguredBucket()
    {
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.DeleteObjectAsync(
                "tiny-lang-media",
                "staging/image.png",
                TestContext.Current.CancellationToken))
            .ReturnsAsync(new DeleteObjectResponse());
        var service = CreateService(s3Client.Object);

        await service.DeleteObjectAsync(
            "staging/image.png",
            TestContext.Current.CancellationToken);

        s3Client.VerifyAll();
    }

    [Fact]
    public async Task ShouldInitiateMultipartUploadWithLockedContentType()
    {
        InitiateMultipartUploadRequest? capturedRequest = null;
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.InitiateMultipartUploadAsync(
                It.IsAny<InitiateMultipartUploadRequest>(),
                TestContext.Current.CancellationToken))
            .Callback<InitiateMultipartUploadRequest, CancellationToken>(
                (request, _) => capturedRequest = request)
            .ReturnsAsync(new InitiateMultipartUploadResponse
            {
                UploadId = "provider-upload-id"
            });
        var service = CreateService(s3Client.Object);

        var uploadId = await service.CreateMultipartUploadAsync(
            "staging/video.mp4",
            "video/mp4",
            TestContext.Current.CancellationToken);

        uploadId.Should().Be("provider-upload-id");
        capturedRequest!.BucketName.Should().Be("tiny-lang-media");
        capturedRequest.Key.Should().Be("staging/video.mp4");
        capturedRequest.ContentType.Should().Be("video/mp4");
    }

    [Fact]
    public async Task ShouldPresignMultipartPartWithProviderCoordinatesAndLength()
    {
        GetPreSignedUrlRequest? capturedRequest = null;
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.GetPreSignedURLAsync(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => capturedRequest = request)
            .ReturnsAsync("https://s3.example.com/part");
        var service = CreateService(s3Client.Object);

        await service.PresignUploadPartAsync(
            "staging/video.mp4",
            "provider-upload-id",
            3,
            16 * 1024 * 1024,
            DateTimeOffset.UtcNow.AddHours(1),
            TestContext.Current.CancellationToken);

        capturedRequest!.BucketName.Should().Be("tiny-lang-media");
        capturedRequest.Key.Should().Be("staging/video.mp4");
        capturedRequest.UploadId.Should().Be("provider-upload-id");
        capturedRequest.PartNumber.Should().Be(3);
        capturedRequest.Headers.ContentLength.Should().Be(16 * 1024 * 1024);
    }

    [Fact]
    public async Task ShouldCompleteMultipartUploadWithOrderedProviderParts()
    {
        CompleteMultipartUploadRequest? capturedRequest = null;
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.CompleteMultipartUploadAsync(
                It.IsAny<CompleteMultipartUploadRequest>(),
                TestContext.Current.CancellationToken))
            .Callback<CompleteMultipartUploadRequest, CancellationToken>(
                (request, _) => capturedRequest = request)
            .ReturnsAsync(new CompleteMultipartUploadResponse());
        var service = CreateService(s3Client.Object);

        await service.CompleteMultipartUploadAsync(
            "staging/video.mp4",
            "provider-upload-id",
            [new ObjectStorageUploadedPart(1, "etag-1"), new ObjectStorageUploadedPart(2, "etag-2")],
            TestContext.Current.CancellationToken);

        capturedRequest!.BucketName.Should().Be("tiny-lang-media");
        capturedRequest.Key.Should().Be("staging/video.mp4");
        capturedRequest.UploadId.Should().Be("provider-upload-id");
        capturedRequest.PartETags.Select(part => part.PartNumber).Should().Equal(1, 2);
        capturedRequest.PartETags.Select(part => part.ETag).Should().Equal("etag-1", "etag-2");
    }

    [Fact]
    public async Task ShouldMapProviderInvalidPartWithoutExposingProviderMessage()
    {
        var providerException = new AmazonS3Exception("sensitive provider response")
        {
            ErrorCode = "InvalidPart"
        };
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.CompleteMultipartUploadAsync(
                It.IsAny<CompleteMultipartUploadRequest>(),
                TestContext.Current.CancellationToken))
            .ThrowsAsync(providerException);
        var service = CreateService(s3Client.Object);

        var action = () => service.CompleteMultipartUploadAsync(
            "staging/video.mp4",
            "provider-upload-id",
            [new ObjectStorageUploadedPart(1, "etag-1")],
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ObjectStorageProtocolException>()
            .Where(exception =>
                exception.Reason == ObjectStorageProtocolError.InvalidParts &&
                !exception.Message.Contains("sensitive", StringComparison.Ordinal));
    }

    [Fact]
    public void ShouldBuildEncodedPublicUrl()
    {
        var service = CreateService(Mock.Of<IAmazonS3>());

        var result = service.GetPublicUrl("avatars/2026/07/profile picture.png");

        result.Should().Be(
            "https://oss.example.com/media/avatars/2026/07/profile%20picture.png");
    }

    private static S3ObjectStorageService CreateService(IAmazonS3 s3Client)
        => new(s3Client, Options.Create(new ObjectStorageSettings
        {
            Region = "us-east-1",
            Bucket = "tiny-lang-media",
            PublicBaseUrl = "https://oss.example.com/media",
            PresignedUrlExpirySeconds = 900
        }));
}
