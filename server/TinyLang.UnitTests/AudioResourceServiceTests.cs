using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class AudioResourceServiceTests
{
    [Fact]
    public async Task SimpleInitializationShouldCreateUploadingAudioResource()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = new MediaResource
        {
            UploaderId = adminId,
            ObjectName = "audio/final.mp3",
            StagingObjectName = "staging/audio.mp3",
            OriginalName = "lesson.mp3",
            Module = ResourceModule.Audio,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var mediaId = source.Id;
        db.MediaResources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.CreatePendingResourceAndPresignAsync(
                adminId,
                "lesson.mp3",
                ".mp3",
                1024,
                "audio/mpeg",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaResourcePresignResult(
                mediaId,
                "https://media.example/upload",
                "staging/audio.mp3"));
        var service = new AudioResourceService(
            db,
            media.Object,
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System);

        var response = await service.InitializeSimpleUploadAsync(
            adminId,
            new InitializeAudioUploadRequest
            {
                OriginalName = "lesson.mp3",
                Extension = ".mp3",
                ContentType = "audio/mpeg",
                Size = 1024
            },
            TestContext.Current.CancellationToken);

        var saved = await db.AudioResources.SingleAsync(
            TestContext.Current.CancellationToken);
        response.AudioResourceId.Should().Be(saved.Id);
        response.MediaResourceId.Should().Be(mediaId);
        saved.Status.Should().Be(AudioResourceStatus.Uploading);
        saved.Name.Should().Be("lesson.mp3");
    }

    [Fact]
    public async Task DuplicateNamesShouldReceiveNextAvailableSequence()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var media = new Mock<IMediaResourceService>();
        media.SetupSequence(value => value.CreatePendingResourceAndPresignAsync(
                adminId,
                It.IsAny<string>(),
                ".mp3",
                1024,
                "audio/mpeg",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MediaResourcePresignResult(
                Guid.NewGuid(), "https://media.example/1", "staging/1.mp3"))
            .ReturnsAsync(() => new MediaResourcePresignResult(
                Guid.NewGuid(), "https://media.example/2", "staging/2.mp3"));
        var service = new AudioResourceService(
            db,
            media.Object,
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System);

        await service.InitializeSimpleUploadAsync(adminId, new InitializeAudioUploadRequest
        {
            OriginalName = "lesson.mp3",
            Extension = ".mp3",
            ContentType = "audio/mpeg",
            Size = 1024
        }, TestContext.Current.CancellationToken);
        await service.InitializeSimpleUploadAsync(adminId, new InitializeAudioUploadRequest
        {
            OriginalName = "lesson.mp3",
            Extension = ".mp3",
            ContentType = "audio/mpeg",
            Size = 1024
        }, TestContext.Current.CancellationToken);

        (await db.AudioResources
                .OrderBy(value => value.Name)
                .Select(value => value.Name)
                .ToListAsync(TestContext.Current.CancellationToken))
            .Should().Equal("lesson (2).mp3", "lesson.mp3");
    }

    [Fact]
    public async Task DuplicateMaximumLengthNameShouldKeepGeneratedNameWithinLimit()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var originalName = $"{new string('a', 251)}.mp3";
        var media = new Mock<IMediaResourceService>();
        media.SetupSequence(value => value.CreatePendingResourceAndPresignAsync(
                adminId,
                originalName,
                ".mp3",
                1024,
                "audio/mpeg",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MediaResourcePresignResult(
                Guid.NewGuid(), "https://media.example/1", "staging/1.mp3"))
            .ReturnsAsync(() => new MediaResourcePresignResult(
                Guid.NewGuid(), "https://media.example/2", "staging/2.mp3"));
        var service = CreateService(db, media.Object);
        var request = new InitializeAudioUploadRequest
        {
            OriginalName = originalName,
            Extension = ".mp3",
            ContentType = "audio/mpeg",
            Size = 1024
        };

        await service.InitializeSimpleUploadAsync(
            adminId, request, TestContext.Current.CancellationToken);
        await service.InitializeSimpleUploadAsync(
            adminId, request, TestContext.Current.CancellationToken);

        var generatedName = await db.AudioResources
            .Where(value => value.Name != originalName)
            .Select(value => value.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        generatedName.Should().HaveLength(255);
        generatedName.Should().EndWith(" (2).mp3");
    }

    [Fact]
    public async Task ConfirmUploadShouldQueueExactlyOneProcessingJob()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        db.MediaResources.Add(source);
        db.AudioResources.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.ConfirmAsync(
                source.Id,
                adminId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);
        var service = CreateService(db, media.Object);

        await service.ConfirmUploadAsync(
            audio.Id, adminId, TestContext.Current.CancellationToken);
        await service.ConfirmUploadAsync(
            audio.Id, adminId, TestContext.Current.CancellationToken);

        audio.Status.Should().Be(AudioResourceStatus.Queued);
        (await db.AudioProcessingJobs.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(1);
        media.Verify(value => value.ConfirmAsync(
            source.Id, adminId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmIncompleteUploadShouldUseAudioDomainError()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        db.MediaResources.Add(source);
        db.AudioResources.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.ConfirmAsync(
                source.Id,
                adminId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(ConflictException.Create(
                ErrorCodes.MediaResourceUploadIncomplete));
        var service = CreateService(db, media.Object);

        var action = async () => await service.ConfirmUploadAsync(
            audio.Id, adminId, TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AudioUploadIncomplete);
        audio.Status.Should().Be(AudioResourceStatus.Uploading);
        (await db.AudioProcessingJobs.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task AudioMultipartOperationShouldRejectNonAudioSession()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var mediaResource = new MediaResource
        {
            UploaderId = adminId,
            ObjectName = "videos/source.mp4",
            StagingObjectName = "staging/videos/source.mp4",
            OriginalName = "source.mp4",
            Module = ResourceModule.CourseVideo,
            Size = 64L * 1024 * 1024,
            Extension = ".mp4",
            ContentType = "video/mp4"
        };
        var session = new MultipartUploadSession
        {
            MediaResource = mediaResource,
            MediaResourceId = mediaResource.Id,
            UploaderId = adminId,
            ProviderUploadId = "provider-upload",
            PartSize = 16L * 1024 * 1024,
            PartCount = 4,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        db.MediaResources.Add(mediaResource);
        db.MultipartUploadSessions.Add(session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var action = async () => await service.GetMultipartUploadAsync(
            session.Id, adminId, TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<NotFoundException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.MultipartUploadNotFound);
    }

    [Fact]
    public async Task AbortMultipartUploadShouldFailAudioResource()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        source.Status = ResourceStatus.Pending;
        source.StagingObjectName = "staging/audios/source.mp3";
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        var session = new MultipartUploadSession
        {
            MediaResource = source,
            MediaResourceId = source.Id,
            UploaderId = adminId,
            ProviderUploadId = "provider-upload",
            PartSize = 16L * 1024 * 1024,
            PartCount = 4,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        db.AddRange(audio, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.AbortMultipartUploadAsync(
                session.Id,
                adminId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(db, media.Object);

        await service.AbortMultipartUploadAsync(
            session.Id,
            adminId,
            TestContext.Current.CancellationToken);

        audio.Status.Should().Be(AudioResourceStatus.Failed);
        audio.LastFailureCode.Should().Be(ErrorCodes.AudioUploadIncomplete.ToString());
    }

    [Fact]
    public async Task ReprocessShouldQueueNewVersionAndPreserveReadyOutput()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        var oldVersion = Guid.NewGuid();
        var oldOutput = $"audios/{audio.Id:N}/outputs/{oldVersion:N}/audio.mp3";
        audio.SourceMediaResource = source;
        audio.Status = AudioResourceStatus.Failed;
        audio.CurrentOutputVersion = oldVersion;
        audio.OutputObjectName = oldOutput;
        audio.LastFailureCode = "TranscodeFailed";
        db.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var response = await service.ReprocessAsync(
            audio.Id, adminId, TestContext.Current.CancellationToken);

        response.Status.Should().Be(AudioResourceStatus.Queued);
        audio.CurrentOutputVersion.Should().Be(oldVersion);
        audio.OutputObjectName.Should().Be(oldOutput);
        var job = await db.AudioProcessingJobs.SingleAsync(
            TestContext.Current.CancellationToken);
        job.AudioResourceId.Should().Be(audio.Id);
        job.OutputVersion.Should().NotBe(oldVersion);
        job.Status.Should().Be(AudioProcessingJobStatus.Queued);
    }

    [Fact]
    public async Task ReprocessShouldRejectInactiveSourceAsUploadIncomplete()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        source.Status = ResourceStatus.Expired;
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        audio.Status = AudioResourceStatus.Failed;
        db.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var action = async () => await service.ReprocessAsync(
            audio.Id, adminId, TestContext.Current.CancellationToken);

        (await action.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.AudioUploadIncomplete);
        audio.Status.Should().Be(AudioResourceStatus.Failed);
        (await db.AudioProcessingJobs.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task ReprocessShouldQueueInsideLockedTransaction()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        audio.Status = AudioResourceStatus.Failed;
        db.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var transaction = new Mock<IApplicationDbTransaction>();
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.MediaResources).Returns(db.MediaResources);
        context.SetupGet(value => value.AudioProcessingJobs)
            .Returns(db.AudioProcessingJobs);
        context.Setup(value => value.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        context.Setup(value => value.AcquireAudioResourceLockAsync(
                audio.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Setup(value => value.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => db.SaveChangesAsync(token));
        var service = new AudioResourceService(
            context.Object,
            Mock.Of<IMediaResourceService>(),
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System);

        await service.ReprocessAsync(
            audio.Id,
            adminId,
            TestContext.Current.CancellationToken);

        context.Verify(value => value.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(value => value.AcquireAudioResourceLockAsync(
            audio.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(value => value.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryUploadShouldReplaceSourceOnSameResourceAndPreserveReadyOutput()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var oldSource = CreateAudioMediaResource(adminId);
        oldSource.StagingObjectName = "staging/audios/old-source.mp3";
        var newSource = new MediaResource
        {
            UploaderId = adminId,
            ObjectName = "audios/pending.mp3",
            StagingObjectName = "staging/audios/pending.mp3",
            OriginalName = "replacement.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Pending,
            Size = 2048,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var audio = AudioResource.Create(adminId, "lesson.mp3", oldSource.Id);
        var oldVersion = Guid.NewGuid();
        var oldOutput = $"audios/{audio.Id:N}/outputs/{oldVersion:N}/audio.mp3";
        audio.SourceMediaResource = oldSource;
        audio.Status = AudioResourceStatus.Failed;
        audio.CurrentOutputVersion = oldVersion;
        audio.OutputObjectName = oldOutput;
        db.AddRange(oldSource, newSource, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.CreatePendingResourceAndPresignAsync(
                adminId,
                "replacement.mp3",
                ".mp3",
                2048,
                "audio/mpeg",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaResourcePresignResult(
                newSource.Id,
                "https://media.example/retry",
                newSource.StagingObjectName!));
        var service = CreateService(db, media.Object);

        object response = await service.RetryUploadAsync(
            audio.Id,
            adminId,
            new InitializeAudioUploadRequest
            {
                OriginalName = "replacement.mp3",
                Extension = ".mp3",
                ContentType = "audio/mpeg",
                Size = 2048
            },
            TestContext.Current.CancellationToken);

        var upload = response.Should()
            .BeOfType<AudioUploadInitializationResponse>().Subject;
        upload.AudioResourceId.Should().Be(audio.Id);
        upload.MediaResourceId.Should().Be(newSource.Id);
        upload.PresignedUrl.Should().Be("https://media.example/retry");
        audio.SourceMediaResourceId.Should().Be(newSource.Id);
        audio.Status.Should().Be(AudioResourceStatus.Uploading);
        oldSource.Status.Should().Be(ResourceStatus.Expired);
        oldSource.StagingObjectName.Should().Be("staging/audios/old-source.mp3");
        audio.CurrentOutputVersion.Should().Be(oldVersion);
        audio.OutputObjectName.Should().Be(oldOutput);
    }

    [Fact]
    public async Task RetryUploadShouldReplaceSourceInsideLockedTransaction()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var oldSource = CreateAudioMediaResource(adminId);
        var newSource = CreateAudioMediaResource(adminId);
        newSource.Status = ResourceStatus.Pending;
        var audio = AudioResource.Create(adminId, "lesson.mp3", oldSource.Id);
        audio.SourceMediaResource = oldSource;
        audio.Status = AudioResourceStatus.Failed;
        db.AddRange(oldSource, newSource, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var transaction = new Mock<IApplicationDbTransaction>();
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.MediaResources).Returns(db.MediaResources);
        context.Setup(value => value.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        context.Setup(value => value.AcquireAudioResourceLockAsync(
                audio.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Setup(value => value.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => db.SaveChangesAsync(token));
        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.CreatePendingResourceAndPresignAsync(
                adminId,
                "replacement.mp3",
                ".mp3",
                2048,
                "audio/mpeg",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaResourcePresignResult(
                newSource.Id,
                "https://media.example/retry",
                newSource.StagingObjectName!));
        var service = new AudioResourceService(
            context.Object,
            media.Object,
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System);

        await service.RetryUploadAsync(
            audio.Id,
            adminId,
            new InitializeAudioUploadRequest
            {
                OriginalName = "replacement.mp3",
                Extension = ".mp3",
                ContentType = "audio/mpeg",
                Size = 2048
            },
            TestContext.Current.CancellationToken);

        context.Verify(value => value.BeginTransactionAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(value => value.AcquireAudioResourceLockAsync(
            audio.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(value => value.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AudioResourceService CreateService(
        ApplicationDbContext db,
        IMediaResourceService mediaResourceService)
        => new(
            db,
            mediaResourceService,
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System);

    private static MediaResource CreateAudioMediaResource(Guid adminId)
        => new()
        {
            UploaderId = adminId,
            ObjectName = $"audios/{Guid.NewGuid():N}.mp3",
            OriginalName = "lesson.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
}
