using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Database;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Policies;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class MultipartMediaResourceServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ThresholdShouldRequireMultipartAndCreateBoundedSession()
    {
        await using var db = CreateDbContext();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.CreateMultipartUploadAsync(
                It.IsAny<string>(),
                "video/mp4",
                TestContext.Current.CancellationToken))
            .ReturnsAsync("provider-upload-id");
        var service = CreateService(db, storage.Object);
        var size = Megabytes(64);

        var simple = () => service.CreatePendingResourceAndPresignAsync(
            Guid.NewGuid(), "course.mp4", ".mp4", size, "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);
        var multipart = await service.CreateMultipartUploadAsync(
            Guid.NewGuid(), "course.mp4", ".mp4", size, "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);

        await simple.Should().ThrowAsync<RequestValidationException>()
            .WithMessage(ErrorCodes.MultipartUploadRequired.GetMessage());
        multipart.PartSize.Should().Be(Megabytes(16));
        multipart.PartCount.Should().Be(4);
        multipart.ExpiresAt.Should().Be(Now.AddHours(24));
        var resource = await db.MediaResources.SingleAsync(
            TestContext.Current.CancellationToken);
        resource.ObjectName.Should().MatchRegex(
            @"^courses/2026/07/[0-9a-f]{32}\.mp4$");
        resource.StagingObjectName.Should().StartWith($"staging/{resource.Id:N}/");
        (await db.MultipartUploadSessions.SingleAsync(
            TestContext.Current.CancellationToken)).ProviderUploadId
            .Should().Be("provider-upload-id");
    }

    [Fact]
    public async Task PartPresignShouldUseExpectedLengthAndRejectOtherOwner()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.CreateMultipartUploadAsync(
                It.IsAny<string>(), "video/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider-upload-id");
        storage.Setup(x => x.PresignUploadPartAsync(
                It.IsAny<string>(),
                "provider-upload-id",
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string objectName,
                string uploadId,
                int partNumber,
                long contentLength,
                DateTimeOffset expiresAt,
                CancellationToken cancellationToken) => Task.FromResult(
                    $"https://storage.example.com/part/{partNumber}"));
        var service = CreateService(db, storage.Object);
        var created = await service.CreateMultipartUploadAsync(
            ownerId, "course.mp4", ".mp4", Megabytes(65), "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);

        var parts = await service.PresignMultipartPartsAsync(
            created.SessionId,
            ownerId,
            [5, 1],
            TestContext.Current.CancellationToken);
        var forbidden = () => service.PresignMultipartPartsAsync(
            created.SessionId,
            Guid.NewGuid(),
            [1],
            TestContext.Current.CancellationToken);

        parts.Select(x => x.PartNumber).Should().Equal(1, 5);
        parts[0].ContentLength.Should().Be(Megabytes(16));
        parts[1].ContentLength.Should().Be(Megabytes(1));
        await forbidden.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CompleteShouldSortPartsAndBecomeIdempotentlyFinalizing()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        IReadOnlyList<ObjectStorageUploadedPart>? capturedParts = null;
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.CreateMultipartUploadAsync(
                It.IsAny<string>(), "video/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider-upload-id");
        storage.SetupSequence(x => x.GetObjectMetadataAsync(
                It.IsAny<string>(), TestContext.Current.CancellationToken))
            .ReturnsAsync((ObjectStorageMetadata?)null)
            .ReturnsAsync(new ObjectStorageMetadata(Megabytes(64), "video/mp4"));
        storage.Setup(x => x.CompleteMultipartUploadAsync(
                It.IsAny<string>(),
                "provider-upload-id",
                It.IsAny<IReadOnlyList<ObjectStorageUploadedPart>>(),
                TestContext.Current.CancellationToken))
            .Callback<string, string, IReadOnlyList<ObjectStorageUploadedPart>, CancellationToken>(
                (_, _, parts, _) => capturedParts = parts)
            .Returns(Task.CompletedTask);
        var service = CreateService(db, storage.Object);
        var created = await service.CreateMultipartUploadAsync(
            ownerId, "course.mp4", ".mp4", Megabytes(64), "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);
        ObjectStorageUploadedPart[] parts =
        [
            new(4, "etag-4"),
            new(2, "etag-2"),
            new(1, "etag-1"),
            new(3, "etag-3")
        ];

        var completed = await service.CompleteMultipartUploadAsync(
            created.SessionId, ownerId, parts, TestContext.Current.CancellationToken);
        var repeated = await service.CompleteMultipartUploadAsync(
            created.SessionId, ownerId, parts, TestContext.Current.CancellationToken);

        completed.Status.Should().Be(MultipartUploadStatus.Finalizing);
        repeated.Status.Should().Be(MultipartUploadStatus.Finalizing);
        capturedParts!.Select(x => x.PartNumber).Should().Equal(1, 2, 3, 4);
        (await db.MediaResources.SingleAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(ResourceStatus.Finalizing);
        storage.Verify(x => x.CompleteMultipartUploadAsync(
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ObjectStorageUploadedPart>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AbortShouldBeIdempotentAndPreventComplete()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.CreateMultipartUploadAsync(
                It.IsAny<string>(), "video/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider-upload-id");
        storage.Setup(x => x.AbortMultipartUploadAsync(
                It.IsAny<string>(), "provider-upload-id", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(x => x.DeleteObjectAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(db, storage.Object);
        var created = await service.CreateMultipartUploadAsync(
            ownerId, "course.mp4", ".mp4", Megabytes(64), "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);

        await service.AbortMultipartUploadAsync(
            created.SessionId, ownerId, TestContext.Current.CancellationToken);
        await service.AbortMultipartUploadAsync(
            created.SessionId, ownerId, TestContext.Current.CancellationToken);
        var complete = () => service.CompleteMultipartUploadAsync(
            created.SessionId,
            ownerId,
            [new ObjectStorageUploadedPart(1, "etag")],
            TestContext.Current.CancellationToken);

        (await db.MediaResources.SingleAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(ResourceStatus.Aborted);
        await complete.Should().ThrowAsync<ConflictException>();
        storage.Verify(x => x.AbortMultipartUploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExpiredSessionShouldRejectPartSigning()
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider(Now);
        var ownerId = Guid.NewGuid();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.CreateMultipartUploadAsync(
                It.IsAny<string>(), "video/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider-upload-id");
        var service = CreateService(db, storage.Object, clock);
        var created = await service.CreateMultipartUploadAsync(
            ownerId, "course.mp4", ".mp4", Megabytes(64), "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(25));

        var action = () => service.PresignMultipartPartsAsync(
            created.SessionId,
            ownerId,
            [1],
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.MultipartUploadExpired.GetMessage());
    }

    [Fact]
    public async Task ConfirmShouldNotSynchronouslyArchivePendingMultipartResource()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(x => x.CreateMultipartUploadAsync(
                It.IsAny<string>(), "video/mp4", It.IsAny<CancellationToken>()))
            .ReturnsAsync("provider-upload-id");
        var service = CreateService(db, storage.Object);
        var created = await service.CreateMultipartUploadAsync(
            ownerId, "course.mp4", ".mp4", Megabytes(64), "video/mp4",
            ResourceModule.CourseVideo, TestContext.Current.CancellationToken);

        var action = () => service.ConfirmAsync(
            created.ResourceId,
            ownerId,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>()
            .WithMessage(ErrorCodes.MediaResourceUploadIncomplete.GetMessage());
        storage.Verify(x => x.CopyObjectAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static MediaResourceService CreateService(
        ApplicationDbContext db,
        IObjectStorageService storage,
        TimeProvider? timeProvider = null)
        => new(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MediaResourceService>.Instance,
            db,
            storage,
            new MediaUploadPolicy(Options.Create(TestUploadSettings.Create())),
            Options.Create(TestMultipartUploadSettings.Create()),
            timeProvider ?? new TestTimeProvider(Now));

    private static long Megabytes(int value) => (long)value * 1024 * 1024;
}
