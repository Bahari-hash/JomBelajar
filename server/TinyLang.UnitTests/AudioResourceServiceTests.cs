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
