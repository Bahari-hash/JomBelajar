using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Policies;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class MediaResourceServiceTests
{
    [Fact]
    public async Task ShouldCreatePendingResourceWithImmutableFinalAndStagingNames()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var resources = new Mock<DbSet<MediaResource>>();
        MediaResource? savedResource = null;
        resources.Setup(x => x.Add(It.IsAny<MediaResource>()))
            .Callback<MediaResource>(resource => savedResource = resource);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.PresignPutObjectAsync(
                It.IsAny<string>(),
                "image/webp",
                1024,
                cancellationToken))
            .ReturnsAsync("https://storage.example.com/presigned");
        var service = CreateService(db.Object, storage.Object);

        var result = await service.CreatePendingResourceAndPresignAsync(
            Guid.NewGuid(),
            "avatar.webp",
            ".webp",
            1024,
            "IMAGE/WEBP",
            ResourceModule.Avatar,
            cancellationToken);

        savedResource.Should().NotBeNull();
        savedResource!.ObjectName.Should().MatchRegex(
            @"^avatars/[0-9]{4}/[0-9]{2}/[0-9a-f]{32}\.webp$");
        savedResource.StagingObjectName.Should().MatchRegex(
            @"^staging/[0-9a-f]{32}/[0-9a-f]{32}\.webp$");
        savedResource.StagingObjectName.Should().Be(result.ObjectName);
        savedResource.ContentType.Should().Be("image/webp");
        savedResource.Status.Should().Be(ResourceStatus.Pending);
        savedResource.Url.Should().BeNull();
        result.PresignedUrl.Should().Be("https://storage.example.com/presigned");
        db.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ShouldRejectUnsafeInputBeforePresigningOrPersisting()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var resources = new Mock<DbSet<MediaResource>>();
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        var service = CreateService(db.Object, storage.Object);

        var invalidExtension = async () => await service.CreatePendingResourceAndPresignAsync(
            Guid.NewGuid(), "avatar.svg", ".svg", 1024, "image/svg+xml",
            ResourceModule.Avatar, cancellationToken);
        var invalidContentType = async () => await service.CreatePendingResourceAndPresignAsync(
            Guid.NewGuid(), "avatar.png", ".png", 1024, "text/html",
            ResourceModule.Avatar, cancellationToken);
        var oversized = async () => await service.CreatePendingResourceAndPresignAsync(
            Guid.NewGuid(), "avatar.png", ".png", Megabytes(5) + 1, "image/png",
            ResourceModule.Avatar, cancellationToken);

        await invalidExtension.Should().ThrowAsync<RequestValidationException>();
        await invalidContentType.Should().ThrowAsync<RequestValidationException>();
        await oversized.Should().ThrowAsync<RequestValidationException>();
        storage.Verify(x => x.PresignPutObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
        resources.Verify(x => x.Add(It.IsAny<MediaResource>()), Times.Never);
    }

    [Fact]
    public async Task ShouldWrapPresigningFailureAfterPersistingCleanupRecord()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var resources = new Mock<DbSet<MediaResource>>();
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.PresignPutObjectAsync(
                It.IsAny<string>(), "image/png", 1024, cancellationToken))
            .ThrowsAsync(new InvalidOperationException("Storage unavailable."));
        var service = CreateService(db.Object, storage.Object);

        var action = async () => await service.CreatePendingResourceAndPresignAsync(
            Guid.NewGuid(), "avatar.png", ".png", 1024, "image/png",
            ResourceModule.Avatar, cancellationToken);

        await action.Should().ThrowAsync<UnexpectedException>();
        resources.Verify(x => x.Add(It.IsAny<MediaResource>()), Times.Once);
        db.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ShouldMoveTemporaryObjectAndActivateResourceOnConfirmation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(resource.ObjectName, cancellationToken))
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.Setup(x => x.CopyObjectAsync(
                resource.ObjectName,
                It.IsAny<string>(),
                cancellationToken))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(key => $"https://cdn.example.com/{key}");
        var service = CreateService(db.Object, storage.Object);
        var expectedObjectName = $"avatars/2026/07/{resource.Id:N}.png";

        var confirmed = await service.ConfirmAsync(
            resource.Id,
            uploaderId,
            cancellationToken);

        confirmed.Status.Should().Be(ResourceStatus.Active);
        confirmed.ObjectName.Should().Be(expectedObjectName);
        confirmed.Url.Should().Be($"https://cdn.example.com/{expectedObjectName}");
        storage.Verify(x => x.CopyObjectAsync(
            "temp/upload.png",
            expectedObjectName,
            cancellationToken), Times.Once);
        storage.Verify(x => x.DeleteObjectAsync(
            "temp/upload.png",
            cancellationToken), Times.Once);
        db.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ShouldRejectConfirmationByDifferentUploader()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var resource = CreatePendingResource(Guid.NewGuid());
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        var service = CreateService(db.Object, storage.Object);

        var action = async () => await service.ConfirmAsync(
            resource.Id,
            Guid.NewGuid(),
            cancellationToken);

        await action.Should().ThrowAsync<ForbiddenException>();
        storage.Verify(x => x.GetObjectMetadataAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldRejectConfirmationWhenTemporaryObjectIsMissing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(resource.ObjectName, cancellationToken))
            .ReturnsAsync((ObjectStorageMetadata?)null);
        var service = CreateService(db.Object, storage.Object);

        var action = async () => await service.ConfirmAsync(
            resource.Id,
            uploaderId,
            cancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
        storage.Verify(x => x.CopyObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(1023L, "image/png")]
    [InlineData(1024L, "text/html")]
    public async Task ShouldRejectConfirmationWhenStoredMetadataDoesNotMatch(
        long storedSize,
        string storedContentType)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(resource.ObjectName, cancellationToken))
            .ReturnsAsync(new ObjectStorageMetadata(storedSize, storedContentType));
        var service = CreateService(db.Object, storage.Object);

        var action = async () => await service.ConfirmAsync(
            resource.Id,
            uploaderId,
            cancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
        storage.Verify(x => x.CopyObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldNotActivateResourceWhenMoveFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(resource.ObjectName, cancellationToken))
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.Setup(x => x.CopyObjectAsync(
                resource.ObjectName,
                It.IsAny<string>(),
                cancellationToken))
            .ThrowsAsync(new InvalidOperationException("Move failed."));
        var service = CreateService(db.Object, storage.Object);

        var action = async () => await service.ConfirmAsync(
            resource.Id,
            uploaderId,
            cancellationToken);

        await action.Should().ThrowAsync<UnexpectedException>();
        resource.Status.Should().Be(ResourceStatus.Pending);
        resource.ObjectName.Should().Be("temp/upload.png");
        resource.Url.Should().BeNull();
        db.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldRecoverWhenFinalObjectExistsButTemporaryObjectIsMissing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        var finalObjectName = $"avatars/2026/07/{resource.Id:N}.png";
        storage.Setup(x => x.GetObjectMetadataAsync(resource.ObjectName, cancellationToken))
            .ReturnsAsync((ObjectStorageMetadata?)null);
        storage.Setup(x => x.GetObjectMetadataAsync(finalObjectName, cancellationToken))
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.Setup(x => x.GetPublicUrl(finalObjectName))
            .Returns($"https://cdn.example.com/{finalObjectName}");
        var service = CreateService(db.Object, storage.Object);

        var confirmed = await service.ConfirmAsync(
            resource.Id,
            uploaderId,
            cancellationToken);

        confirmed.Status.Should().Be(ResourceStatus.Active);
        confirmed.ObjectName.Should().Be(finalObjectName);
        storage.Verify(x => x.CopyObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(x => x.SaveChangesAsync(cancellationToken), Times.Once);
    }

    [Fact]
    public async Task ShouldReturnAlreadyActiveResourceIdempotently()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        resource.Status = ResourceStatus.Active;
        resource.ObjectName = $"avatars/2026/07/{resource.Id:N}.png";
        resource.Url = $"https://cdn.example.com/{resource.ObjectName}";
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        var service = CreateService(db.Object, storage.Object);

        var confirmed = await service.ConfirmAsync(
            resource.Id,
            uploaderId,
            cancellationToken);

        confirmed.Should().BeSameAs(resource);
        storage.VerifyNoOtherCalls();
        db.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldKeepStagingNameWhenDeleteFailsAfterSuccessfulCopy()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var uploaderId = Guid.NewGuid();
        var resource = CreatePendingResource(uploaderId);
        var resources = CreateResourceSet(resource, cancellationToken);
        var db = CreateDb(resources, cancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.GetObjectMetadataAsync(resource.ObjectName, cancellationToken))
            .ReturnsAsync(new ObjectStorageMetadata(resource.Size, resource.ContentType));
        storage.Setup(x => x.CopyObjectAsync(
                resource.ObjectName, It.IsAny<string>(), cancellationToken))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.DeleteObjectAsync(resource.ObjectName, cancellationToken))
            .ThrowsAsync(new InvalidOperationException("Delete failed."));
        storage.Setup(x => x.GetPublicUrl(It.IsAny<string>()))
            .Returns<string>(key => $"https://cdn.example.com/{key}");
        var service = CreateService(db.Object, storage.Object);

        var confirmed = await service.ConfirmAsync(
            resource.Id, uploaderId, cancellationToken);

        confirmed.Status.Should().Be(ResourceStatus.Active);
        confirmed.StagingObjectName.Should().Be("temp/upload.png");
    }

    private static MediaResourceService CreateService(
        IApplicationDbContext db,
        IObjectStorageService storage)
        => new(
            db,
            storage,
            new MediaUploadPolicy(Options.Create(TestUploadSettings.Create())),
            Options.Create(TestMultipartUploadSettings.Create()),
            TimeProvider.System);

    private static Mock<IApplicationDbContext> CreateDb(
        Mock<DbSet<MediaResource>> resources,
        CancellationToken cancellationToken)
    {
        var db = new Mock<IApplicationDbContext>();
        db.SetupGet(x => x.MediaResources).Returns(resources.Object);
        db.Setup(x => x.SaveChangesAsync(cancellationToken)).ReturnsAsync(1);
        return db;
    }

    private static Mock<DbSet<MediaResource>> CreateResourceSet(
        MediaResource resource,
        CancellationToken cancellationToken)
    {
        var resources = new Mock<DbSet<MediaResource>>();
        resources.Setup(x => x.FindAsync(new object?[] { resource.Id }, cancellationToken))
            .Returns(new ValueTask<MediaResource?>(resource));
        return resources;
    }

    private static MediaResource CreatePendingResource(Guid uploaderId)
        => new()
        {
            UploaderId = uploaderId,
            ObjectName = "temp/upload.png",
            OriginalName = "avatar.png",
            Module = ResourceModule.Avatar,
            Size = 1024,
            Extension = ".png",
            ContentType = "image/png",
            Url = null,
            CreatedAt = new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)
        };

    private static long Megabytes(int value) => (long)value * 1024 * 1024;
}
