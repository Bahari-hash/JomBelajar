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

/// <summary>
/// 验证音频 source 绑定、全局管理状态机和播放可见性。
/// </summary>
public sealed class AudioClipServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证管理员可用其他管理员上传的 Active Audio source 原子创建音频和首个 job。
    /// </summary>
    [Fact]
    public async Task ActiveAudioSourceShouldCreateClipAndJobAcrossAdmins()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var source = CreateResource(Guid.NewGuid(), ResourceModule.Audio, ResourceStatus.Active);
        db.MediaResources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateAsync(adminId, new CreateAudioClipRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "  Hello  ",
            Description = " native ",
            LanguageTag = "EN-US",
            Kind = AudioClipKind.WordPronunciation
        }, TestContext.Current.CancellationToken);

        response.Title.Should().Be("Hello");
        response.Description.Should().Be("native");
        response.LanguageTag.Should().Be("en-us");
        response.ProcessingStatus.Should().Be(AudioProcessingStatus.Queued);
        var job = await db.AudioProcessingJobs.SingleAsync(
            TestContext.Current.CancellationToken);
        job.AudioClipId.Should().Be(response.Id);
        job.OutputVersion.Should().NotBeEmpty();
        var audioClip = await db.AudioClips.SingleAsync(TestContext.Current.CancellationToken);
        audioClip.CreatedById.Should().Be(adminId);
        audioClip.LastEditorId.Should().Be(adminId);
    }

    /// <summary>
    /// 验证非 Audio 模块 source 按无效 source 隐藏。
    /// </summary>
    [Fact]
    public async Task InvalidSourceModuleShouldBeRejected()
    {
        await using var db = CreateDbContext();
        var adminId = Guid.NewGuid();
        var source = CreateResource(
            Guid.NewGuid(),
            ResourceModule.CourseVideo,
            ResourceStatus.Active);
        db.MediaResources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateAsync(adminId, new CreateAudioClipRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "Audio",
            LanguageTag = "en",
            Kind = AudioClipKind.Other
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
        (await db.AudioClips.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(0);
    }

    /// <summary>
    /// 验证尚未 Active 的音频 source 不能创建业务资产。
    /// </summary>
    [Fact]
    public async Task InactiveAudioSourceShouldBeRejected()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var source = CreateResource(ownerId, ResourceModule.Audio, ResourceStatus.Pending);
        db.MediaResources.Add(source);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateAsync(ownerId, new CreateAudioClipRequest
        {
            SourceMediaResourceId = source.Id,
            Title = "Audio",
            LanguageTag = "en",
            Kind = AudioClipKind.Other
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证已经绑定 AudioClip 的 source 不会再次触发处理任务。
    /// </summary>
    [Fact]
    public async Task UsedAudioSourceShouldBeRejected()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var existing = CreateAudioClip(ownerId);
        db.AudioClips.Add(existing);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.CreateAsync(ownerId, new CreateAudioClipRequest
        {
            SourceMediaResourceId = existing.SourceMediaResourceId,
            Title = "Duplicate",
            LanguageTag = "en",
            Kind = AudioClipKind.Other
        }, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
        (await db.AudioClips.CountAsync(TestContext.Current.CancellationToken))
            .Should().Be(1);
    }

    /// <summary>
    /// 验证管理员列表跨创建者应用用途筛选并返回确定结果。
    /// </summary>
    [Fact]
    public async Task AdminListShouldIncludeAllCreatorsAndApplyKindFilter()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var expected = CreateAudioClip(ownerId);
        expected.Kind = AudioClipKind.Dialogue;
        var otherKind = CreateAudioClip(ownerId);
        otherKind.Kind = AudioClipKind.Other;
        var otherOwner = CreateAudioClip(Guid.NewGuid());
        otherOwner.Kind = AudioClipKind.Dialogue;
        db.AddRange(expected, otherKind, otherOwner);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetAdminListAsync(
            new AdminAudioClipListRequest { Kind = AudioClipKind.Dialogue },
            TestContext.Current.CancellationToken);

        response.Items.Select(value => value.Id)
            .Should().BeEquivalentTo([expected.Id, otherOwner.Id]);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task AnotherAdminShouldUpdateGlobalAudioAndBecomeLastEditor()
    {
        await using var db = CreateDbContext();
        var creatorId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var audioClip = CreateAudioClip(creatorId);
        db.AudioClips.Add(audioClip);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.UpdateAsync(audioClip.Id, adminId, new UpdateAudioClipRequest
        {
            Title = "Updated",
            LanguageTag = "en",
            Kind = AudioClipKind.Dialogue
        }, TestContext.Current.CancellationToken);

        audioClip.CreatedById.Should().Be(creatorId);
        audioClip.LastEditorId.Should().Be(adminId);
        audioClip.Title.Should().Be("Updated");
    }

    /// <summary>
    /// 验证只有 Ready 音频可发布且发布和下架操作幂等。
    /// </summary>
    [Fact]
    public async Task ReadyAudioPublishAndUnpublishShouldBeIdempotent()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var audioClip = CreateAudioClip(ownerId);
        audioClip.ProcessingStatus = AudioProcessingStatus.Ready;
        db.AudioClips.Add(audioClip);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        await service.PublishAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);
        var published = await service.PublishAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);
        await service.UnpublishAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);

        published.PublishedAt.Should().Be(Now);
        unpublished.PublicationStatus.Should().Be(AudioPublicationStatus.Unpublished);
    }

    /// <summary>
    /// 验证失败音频重试创建新 output version 并回到 queued。
    /// </summary>
    [Fact]
    public async Task FailedAudioRetryShouldCreateNewOutputVersion()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var audioClip = CreateAudioClip(ownerId);
        audioClip.ProcessingStatus = AudioProcessingStatus.Failed;
        audioClip.LastFailureCode = "ProbeFailed";
        db.AudioClips.Add(audioClip);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.RetryAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);

        response.ProcessingStatus.Should().Be(AudioProcessingStatus.Queued);
        response.FailureCode.Should().BeNull();
        (await db.AudioProcessingJobs.SingleAsync(
            TestContext.Current.CancellationToken)).OutputVersion.Should().NotBeEmpty();
    }

    /// <summary>
    /// 验证非 Ready 发布和非 Failed 重试均返回稳定状态冲突。
    /// </summary>
    [Fact]
    public async Task InvalidPublishAndRetryStatesShouldConflict()
    {
        await using var db = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var audioClip = CreateAudioClip(ownerId);
        db.AudioClips.Add(audioClip);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var publish = () => service.PublishAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);
        var retry = () => service.RetryAsync(
            audioClip.Id,
            ownerId,
            TestContext.Current.CancellationToken);

        await publish.Should().ThrowAsync<ConflictException>();
        await retry.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证只有 Ready 和 Published 音频返回短期版本化 MP3 地址。
    /// </summary>
    [Fact]
    public async Task ReadyPublishedAudioShouldReturnPlaybackUrl()
    {
        await using var db = CreateDbContext();
        var audioClip = CreateAudioClip(Guid.NewGuid());
        var outputVersion = Guid.NewGuid();
        var prefix = $"audios/{audioClip.Id:N}/outputs/{outputVersion:N}/";
        audioClip.ProcessingStatus = AudioProcessingStatus.Ready;
        audioClip.PublicationStatus = AudioPublicationStatus.Published;
        audioClip.DurationSeconds = 3.5;
        audioClip.CurrentOutputVersion = outputVersion;
        audioClip.OutputObjectName = $"{prefix}audio.mp3";
        db.AudioClips.Add(audioClip);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var delivery = new Mock<IVideoDeliveryUrlService>();
        delivery.Setup(value => value.CreateUrl(audioClip.OutputObjectName, prefix))
            .Returns(new VideoDeliveryUrl("https://media.example/audio.mp3", Now.AddMinutes(5)));
        var service = CreateService(db, delivery.Object);

        var response = await service.GetPlaybackAsync(
            audioClip.Id,
            TestContext.Current.CancellationToken);

        response.Url.Should().Be("https://media.example/audio.mp3");
        response.DurationSeconds.Should().Be(3.5);
        response.AudioClipKind.Should().Be(AudioClipKind.Other);
        delivery.Verify(value => value.CreateUrl(audioClip.OutputObjectName, prefix), Times.Once);
    }

    /// <summary>
    /// 验证非发布或非就绪状态不会向普通用户泄漏音频存在性。
    /// </summary>
    [Fact]
    public async Task DraftAudioShouldBeHiddenFromPlayback()
    {
        await using var db = CreateDbContext();
        var audioClip = CreateAudioClip(Guid.NewGuid());
        db.AudioClips.Add(audioClip);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = () => service.GetPlaybackAsync(
            audioClip.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 使用可替换 delivery 和固定时间创建音频服务。
    /// </summary>
    private static AudioClipService CreateService(
        ApplicationDbContext db,
        IVideoDeliveryUrlService? delivery = null)
        => new(
            db,
            delivery ?? Mock.Of<IVideoDeliveryUrlService>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(Now));

    /// <summary>
    /// 创建隔离的 EF Core InMemory 上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// 创建具有最小管理字段的音频实体。
    /// </summary>
    private static AudioClip CreateAudioClip(Guid ownerId)
    {
        var source = CreateResource(
            ownerId,
            ResourceModule.Audio,
            ResourceStatus.Active);
        return new AudioClip
        {
            CreatedById = ownerId,
            LastEditorId = ownerId,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = "Audio",
            LanguageTag = "en",
            Kind = AudioClipKind.Other
        };
    }

    /// <summary>
    /// 创建指定 owner、模块和状态的上传资源。
    /// </summary>
    private static MediaResource CreateResource(
        Guid uploaderId,
        ResourceModule module,
        ResourceStatus status)
        => new()
        {
            UploaderId = uploaderId,
            ObjectName = $"objects/{Guid.NewGuid():N}",
            OriginalName = "audio.bin",
            Module = module,
            Status = status,
            Size = 1024,
            Extension = ".bin",
            ContentType = "application/octet-stream"
        };
}
