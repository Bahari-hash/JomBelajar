using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
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
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

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
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

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
    public async Task AdminListShouldSearchNormalizedNameAndReturnOriginalFullFileName()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var expectedSource = CreateAudioMediaResource(adminId);
        var otherSource = CreateAudioMediaResource(adminId);
        var expected = AudioResource.Create(
            adminId, "Lesson FINAL.MP3", expectedSource.Id);
        expected.SourceMediaResource = expectedSource;
        expected.Status = AudioResourceStatus.Ready;
        var other = AudioResource.Create(adminId, "dialogue.mp3", otherSource.Id);
        other.SourceMediaResource = otherSource;
        other.Status = AudioResourceStatus.Ready;
        db.AddRange(expectedSource, otherSource, expected, other);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var response = await service.GetAdminListAsync(
            new AdminAudioResourceListRequest
            {
                Keyword = " lesson final ",
                Status = AudioResourceStatus.Ready
            },
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.Name
            .Should().Be("Lesson FINAL.MP3");
    }

    [Fact]
    public async Task RenameShouldPreserveDisplayNameAndUpdateNormalizedName()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var response = await service.RenameAsync(
            audio.Id,
            adminId,
            new RenameAudioResourceRequest { Name = "Lesson FINAL.MP3" },
            TestContext.Current.CancellationToken);

        response.Name.Should().Be("Lesson FINAL.MP3");
        audio.NormalizedName.Should().Be("lesson final.mp3");
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
        var storage = new Mock<IObjectStorageService>();
        var service = CreateService(db, media.Object, storage.Object);

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
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

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
        var storage = new Mock<IObjectStorageService>();
        var service = CreateService(db, media.Object, storage.Object);

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
        (await db.MediaResources.AnyAsync(
            value => value.Id == oldSource.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        storage.Verify(value => value.DeleteObjectAsync(
            oldSource.ObjectName,
            It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(value => value.DeleteObjectAsync(
            oldSource.StagingObjectName,
            It.IsAny<CancellationToken>()), Times.Once);
        audio.CurrentOutputVersion.Should().Be(oldVersion);
        audio.OutputObjectName.Should().Be(oldOutput);
    }

    [Fact]
    public async Task RetryUploadShouldUseMultipartWhenSimpleUploadRequiresIt()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var oldSource = CreateAudioMediaResource(adminId);
        var newSource = new MediaResource
        {
            UploaderId = adminId,
            ObjectName = "audios/pending.wav",
            StagingObjectName = "staging/audios/pending.wav",
            OriginalName = "replacement.wav",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Pending,
            Size = 10 * 1024 * 1024,
            Extension = ".wav",
            ContentType = "audio/wav"
        };
        var audio = AudioResource.Create(adminId, "lesson.mp3", oldSource.Id);
        audio.SourceMediaResource = oldSource;
        audio.Status = AudioResourceStatus.Failed;
        db.AddRange(oldSource, newSource, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sessionId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var media = new Mock<IMediaResourceService>();
        media.Setup(value => value.CreatePendingResourceAndPresignAsync(
                adminId,
                "replacement.wav",
                ".wav",
                10 * 1024 * 1024,
                "audio/wav",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestValidationException(
                ErrorCodes.MultipartUploadRequired));
        media.Setup(value => value.CreateMultipartUploadAsync(
                adminId,
                "replacement.wav",
                ".wav",
                10 * 1024 * 1024,
                "audio/wav",
                ResourceModule.Audio,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MultipartUploadCreateResult(
                newSource.Id,
                sessionId,
                5 * 1024 * 1024,
                2,
                expiresAt));
        var service = CreateService(db, media.Object);

        var upload = await service.RetryUploadAsync(
            audio.Id,
            adminId,
            new InitializeAudioUploadRequest
            {
                OriginalName = "replacement.wav",
                Extension = ".wav",
                ContentType = "audio/wav",
                Size = 10 * 1024 * 1024
            },
            TestContext.Current.CancellationToken);

        upload.AudioResourceId.Should().Be(audio.Id);
        upload.MediaResourceId.Should().Be(newSource.Id);
        upload.PresignedUrl.Should().BeNull();
        upload.MultipartSessionId.Should().Be(sessionId);
        upload.PartSize.Should().Be(5 * 1024 * 1024);
        upload.PartCount.Should().Be(2);
        upload.ExpiresAt.Should().Be(expiresAt);
        audio.SourceMediaResourceId.Should().Be(newSource.Id);
        audio.Status.Should().Be(AudioResourceStatus.Uploading);
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
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

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

    [Fact]
    public async Task DeleteShouldRemoveResourceJobsAndSourceMediaRecord()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        var job = new AudioProcessingJob
        {
            AudioResourceId = audio.Id,
            AudioResource = audio,
            OutputVersion = Guid.NewGuid()
        };
        db.AddRange(source, audio, job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        await service.DeleteAsync(audio.Id, TestContext.Current.CancellationToken);

        (await db.AudioResources.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(0);
        (await db.AudioProcessingJobs.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(0);
        (await db.MediaResources.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(0);
    }

    [Theory]
    [InlineData("FK_articles_audio_resources_ReadingAudioResourceId")]
    [InlineData("FK_example_sentences_audio_resources_AudioResourceId")]
    public async Task DeleteShouldMapPostgresRestrictViolationToAudioInUse(
        string constraintName)
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var postgresException = new PostgresException(
            "update or delete violates RESTRICT",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.RestrictViolation,
            schemaName: "public",
            tableName: "articles",
            constraintName: constraintName);
        var failure = new DbUpdateException("write failed", postgresException);
        var transaction = new Mock<IApplicationDbTransaction>();
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.AudioProcessingJobs).Returns(db.AudioProcessingJobs);
        context.SetupGet(value => value.MediaResources).Returns(db.MediaResources);
        context.Setup(value => value.BeginTransactionAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        context.Setup(value => value.AcquireAudioResourceLockAsync(
                audio.Id,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        context.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var service = new AudioResourceService(
            context.Object,
            Mock.Of<IMediaResourceService>(),
            Mock.Of<IVideoDeliveryUrlService>(),
            new PostgresDatabaseExceptionClassifier(),
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

        var action = () => service.DeleteAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.AudioInUse);
        transaction.Verify(value => value.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteShouldNotMapUnrelatedDatabaseFailureToAudioInUse()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var failure = new DbUpdateException("write failed");
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.AudioProcessingJobs).Returns(db.AudioProcessingJobs);
        context.SetupGet(value => value.MediaResources).Returns(db.MediaResources);
        context.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var classifier = new Mock<IDatabaseExceptionClassifier>();
        var service = new AudioResourceService(
            context.Object,
            Mock.Of<IMediaResourceService>(),
            Mock.Of<IVideoDeliveryUrlService>(),
            classifier.Object,
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

        var action = () => service.DeleteAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<DbUpdateException>()
            .Where(value => ReferenceEquals(value, failure));
        classifier.Verify(value => value.IsForeignKeyConstraintViolation(
            failure,
            It.Is<string[]>(constraints => constraints.Length == 0)), Times.Once);
    }

    [Fact]
    public async Task ReadyPlaybackShouldReturnShortTermDeliveryWithoutObjectName()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        var version = Guid.NewGuid();
        var objectName = $"audios/{audio.Id:N}/outputs/{version:N}/audio.mp3";
        audio.SourceMediaResource = source;
        audio.Status = AudioResourceStatus.Ready;
        audio.DurationSeconds = 3.5;
        audio.CurrentOutputVersion = version;
        audio.OutputObjectName = objectName;
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var delivery = new Mock<IVideoDeliveryUrlService>();
        delivery.Setup(value => value.CreateTemporaryUrlAsync(
                objectName,
                $"audios/{audio.Id:N}/outputs/{version:N}/",
                It.Is<DateTimeOffset>(value => value > DateTimeOffset.UtcNow),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VideoDeliveryUrl(
                "https://media.example/audio.mp3",
                expiresAt));
        var service = new AudioResourceService(
            db,
            Mock.Of<IMediaResourceService>(),
            delivery.Object,
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System,
            Mock.Of<IObjectStorageService>());

        var response = await service.GetPlaybackAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        response.Should().Be(new AudioResourcePlaybackResponse(
            "https://media.example/audio.mp3",
            expiresAt,
            3.5));
        typeof(AudioResourcePlaybackResponse).GetProperties()
            .Select(value => value.Name)
            .Should().NotContain("ObjectName");
    }

    [Theory]
    [InlineData(AudioResourceStatus.Uploading)]
    [InlineData(AudioResourceStatus.Queued)]
    [InlineData(AudioResourceStatus.Processing)]
    [InlineData(AudioResourceStatus.Failed)]
    public async Task PlaybackShouldRejectNonReadyResource(AudioResourceStatus status)
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        audio.Status = status;
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var action = () => service.GetPlaybackAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.AudioNotReady);
    }

    [Fact]
    public async Task PlaybackShouldRejectOutputOutsideCurrentVersionPrefix()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        audio.Status = AudioResourceStatus.Ready;
        audio.DurationSeconds = 3.5;
        audio.CurrentOutputVersion = Guid.NewGuid();
        audio.OutputObjectName = $"audios/{Guid.NewGuid():N}/outputs/{Guid.NewGuid():N}/audio.mp3";
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, Mock.Of<IMediaResourceService>());

        var action = () => service.GetPlaybackAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.AudioNotReady);
    }

    [Fact]
    public async Task DeleteShouldCleanSourceStagingAndEveryKnownOutputVersionBeforeCommit()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        source.StagingObjectName = $"staging/{source.Id:N}/upload.mp3";
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        var oldVersion = Guid.NewGuid();
        var currentVersion = Guid.NewGuid();
        audio.SourceMediaResource = source;
        audio.CurrentOutputVersion = currentVersion;
        audio.OutputObjectName =
            $"audios/{audio.Id:N}/outputs/{currentVersion:N}/audio.mp3";
        var oldJob = new AudioProcessingJob
        {
            AudioResourceId = audio.Id,
            AudioResource = audio,
            OutputVersion = oldVersion,
            Status = AudioProcessingJobStatus.Failed
        };
        var currentJob = new AudioProcessingJob
        {
            AudioResourceId = audio.Id,
            AudioResource = audio,
            OutputVersion = currentVersion,
            Status = AudioProcessingJobStatus.Completed
        };
        db.AddRange(source, audio, oldJob, currentJob);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var oldObjectName =
            $"audios/{audio.Id:N}/outputs/{oldVersion:N}/audio.mp3";
        var currentObjectName =
            $"audios/{audio.Id:N}/outputs/{currentVersion:N}/audio.mp3";
        var storage = new Mock<IObjectStorageService>();
        var service = CreateServiceWithOptionalStorage(
            db,
            storage.Object);

        await service.DeleteAsync(audio.Id, TestContext.Current.CancellationToken);

        storage.Verify(value => value.DeleteObjectAsync(
            source.ObjectName, It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(value => value.DeleteObjectAsync(
            source.StagingObjectName, It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(value => value.DeleteObjectAsync(
            oldObjectName, It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(value => value.DeleteObjectAsync(
            currentObjectName, It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(value => value.ListObjectNamesAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteShouldNotCommitWhenOutputStorageCleanupFails()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        var version = Guid.NewGuid();
        audio.SourceMediaResource = source;
        var job = new AudioProcessingJob
        {
            AudioResourceId = audio.Id,
            AudioResource = audio,
            OutputVersion = version,
            Status = AudioProcessingJobStatus.Failed
        };
        db.AddRange(source, audio, job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var outputObjectName =
            $"audios/{audio.Id:N}/outputs/{version:N}/audio.mp3";
        var storage = new Mock<IObjectStorageService>();
        var failure = new InvalidOperationException("storage unavailable");
        storage.Setup(value => value.DeleteObjectAsync(
                outputObjectName,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        var transaction = new Mock<IApplicationDbTransaction>();
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.AudioProcessingJobs)
            .Returns(db.AudioProcessingJobs);
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
        var service = CreateServiceWithOptionalStorage(
            context.Object,
            storage.Object);

        var action = () => service.DeleteAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .Where(value => ReferenceEquals(value, failure));
        transaction.Verify(value => value.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteShouldNotCommitWhenSourceStorageCleanupFails()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var source = CreateAudioMediaResource(adminId);
        var audio = AudioResource.Create(adminId, "lesson.mp3", source.Id);
        audio.SourceMediaResource = source;
        db.AddRange(source, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.DeleteObjectAsync(
                source.ObjectName,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        var transaction = new Mock<IApplicationDbTransaction>();
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.AudioProcessingJobs)
            .Returns(db.AudioProcessingJobs);
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
        var service = CreateServiceWithOptionalStorage(
            context.Object,
            storage.Object);

        var action = () => service.DeleteAsync(
            audio.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
        transaction.Verify(value => value.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AudioResourceService CreateService(
        ApplicationDbContext db,
        IMediaResourceService mediaResourceService,
        IObjectStorageService? objectStorageService = null)
        => new(
            db,
            mediaResourceService,
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System,
            objectStorageService ?? Mock.Of<IObjectStorageService>());

    private static AudioResourceService CreateServiceWithOptionalStorage(
        IApplicationDbContext db,
        IObjectStorageService objectStorage)
        => new(
            db,
            Mock.Of<IMediaResourceService>(),
            Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System,
            objectStorage);

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
