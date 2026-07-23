using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class S3ObjectStorageServiceTests
{
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
    public async Task ShouldCopyBeforeDeletingTemporaryObject()
    {
        var operations = new List<string>();
        var s3Client = new Mock<IAmazonS3>();
        s3Client.Setup(x => x.CopyObjectAsync(
                It.IsAny<CopyObjectRequest>(),
                TestContext.Current.CancellationToken))
            .Callback(() => operations.Add("copy"))
            .ReturnsAsync(new CopyObjectResponse());
        s3Client.Setup(x => x.DeleteObjectAsync(
                "tiny-lang-media",
                "temp/image.png",
                TestContext.Current.CancellationToken))
            .Callback(() => operations.Add("delete"))
            .ReturnsAsync(new DeleteObjectResponse());
        var service = CreateService(s3Client.Object);

        await service.MoveObjectAsync(
            "temp/image.png",
            "avatars/2026/07/image.png",
            TestContext.Current.CancellationToken);

        operations.Should().Equal("copy", "delete");
        s3Client.Verify(x => x.CopyObjectAsync(
            It.Is<CopyObjectRequest>(request =>
                request.SourceBucket == "tiny-lang-media" &&
                request.SourceKey == "temp/image.png" &&
                request.DestinationBucket == "tiny-lang-media" &&
                request.DestinationKey == "avatars/2026/07/image.png"),
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public void ShouldBuildEncodedPublicUrl()
    {
        var service = CreateService(Mock.Of<IAmazonS3>());

        var result = service.GetPublicUrl("avatars/2026/07/profile picture.png");

        result.Should().Be(
            "https://cdn.example.com/media/avatars/2026/07/profile%20picture.png");
    }

    private static S3ObjectStorageService CreateService(IAmazonS3 s3Client)
        => new(s3Client, Options.Create(new ObjectStorageSettings
        {
            Region = "us-east-1",
            Bucket = "tiny-lang-media",
            PublicBaseUrl = "https://cdn.example.com/media",
            PresignedUrlExpirySeconds = 900
        }));
}
