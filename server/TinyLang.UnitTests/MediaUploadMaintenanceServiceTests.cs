using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class MediaUploadMaintenanceServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FinalizeShouldCopyValidateDeleteAndActivateResource()
    {
        await using var db = CreateDbContext();
        var (resource, session) = CreateFinalizingUpload();
        db.AddRange(resource, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(
                resource.ObjectName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObjectStorageMetadata?)null);
        storage.Setup(x => x.GetObjectMetadataAsync(
                resource.StagingObjectName!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.SetupSequence(x => x.GetObjectMetadataAsync(
                resource.ObjectName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ObjectStorageMetadata?)null)
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.Setup(x => x.CopyObjectAsync(
                resource.StagingObjectName!, resource.ObjectName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.DeleteObjectAsync(
                resource.StagingObjectName!, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.GetPublicUrl(resource.ObjectName))
            .Returns($"https://oss.example.com/{resource.ObjectName}");
        var service = CreateService(db, storage.Object);

        var count = await service.FinalizeBatchAsync(
            TestContext.Current.CancellationToken);

        count.Should().Be(1);
        resource.Status.Should().Be(ResourceStatus.Active);
        resource.StagingObjectName.Should().BeNull();
        resource.Url.Should().Be($"https://oss.example.com/{resource.ObjectName}");
        session.Status.Should().Be(MultipartUploadStatus.Completed);
        session.LeaseOwner.Should().BeNull();
    }

    [Fact]
    public async Task FinalizeShouldRecoverWhenFinalObjectAlreadyExists()
    {
        await using var db = CreateDbContext();
        var (resource, session) = CreateFinalizingUpload();
        db.AddRange(resource, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(
                resource.ObjectName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.Setup(x => x.DeleteObjectAsync(
                resource.StagingObjectName!, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.GetPublicUrl(resource.ObjectName))
            .Returns($"https://oss.example.com/{resource.ObjectName}");
        var service = CreateService(db, storage.Object);

        await service.FinalizeBatchAsync(TestContext.Current.CancellationToken);

        resource.Status.Should().Be(ResourceStatus.Active);
        storage.Verify(x => x.CopyObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CleanupShouldExpireMultipartAndSimpleUploadsInBatches()
    {
        await using var db = CreateDbContext();
        var simple = CreateResource(ResourceStatus.Pending, "staging/simple.png");
        simple.UploadExpiresAt = Now.AddMinutes(-1);
        var multipart = CreateResource(ResourceStatus.Pending, "staging/video.mp4");
        multipart.UploadExpiresAt = Now.AddMinutes(-1);
        var session = new MultipartUploadSession
        {
            MediaResource = multipart,
            MediaResourceId = multipart.Id,
            UploaderId = multipart.UploaderId,
            ProviderUploadId = "provider-upload-id",
            PartSize = 16 * 1024 * 1024,
            PartCount = 4,
            ExpiresAt = Now.AddMinutes(-1)
        };
        db.AddRange(simple, multipart, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.AbortMultipartUploadAsync(
                multipart.StagingObjectName!,
                session.ProviderUploadId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.DeleteObjectAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(db, storage.Object);

        var count = await service.CleanupBatchAsync(TestContext.Current.CancellationToken);

        count.Should().Be(2);
        simple.Status.Should().Be(ResourceStatus.Expired);
        multipart.Status.Should().Be(ResourceStatus.Expired);
        session.Status.Should().Be(MultipartUploadStatus.Expired);
        simple.StagingObjectName.Should().BeNull();
        multipart.StagingObjectName.Should().BeNull();
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static MediaUploadMaintenanceService CreateService(
        ApplicationDbContext db,
        IObjectStorageService storage)
        => new(
            db,
            storage,
            Options.Create(TestMultipartUploadSettings.Create()),
            new TestTimeProvider(Now),
            NullLogger<MediaUploadMaintenanceService>.Instance);

    private static (MediaResource Resource, MultipartUploadSession Session)
        CreateFinalizingUpload()
    {
        var resource = CreateResource(ResourceStatus.Finalizing, "staging/video.mp4");
        var session = new MultipartUploadSession
        {
            MediaResource = resource,
            MediaResourceId = resource.Id,
            UploaderId = resource.UploaderId,
            ProviderUploadId = "provider-upload-id",
            Status = MultipartUploadStatus.Finalizing,
            PartSize = 16 * 1024 * 1024,
            PartCount = 4,
            ExpiresAt = Now.AddHours(1),
            NextAttemptAt = Now.AddMinutes(-1)
        };
        return (resource, session);
    }

    private static MediaResource CreateResource(
        ResourceStatus status,
        string stagingObjectName)
        => new()
        {
            UploaderId = Guid.NewGuid(),
            ObjectName = $"courses/2026/07/{Guid.NewGuid():N}.mp4",
            StagingObjectName = stagingObjectName,
            OriginalName = "course.mp4",
            Module = ResourceModule.CourseVideo,
            Status = status,
            Size = 64L * 1024 * 1024,
            Extension = ".mp4",
            ContentType = "video/mp4",
            UploadExpiresAt = Now.AddHours(1),
            CreatedAt = Now,
            UpdatedAt = Now
        };
}
