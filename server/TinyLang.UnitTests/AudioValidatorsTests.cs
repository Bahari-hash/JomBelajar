using FluentAssertions;
using Microsoft.Extensions.Options;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Policies;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频资源上传、重命名和列表请求的字段边界。
/// </summary>
public sealed class AudioValidatorsTests
{
    [Fact]
    public void UploadInitializationResponseShouldNotExposeObjectStorageName()
    {
        typeof(AudioUploadInitializationResponse)
            .GetProperty("ObjectName")
            .Should().BeNull();
    }

    [Fact]
    public async Task InitializeUploadShouldApplyAudioUploadPolicy()
    {
        var validator = new InitializeAudioUploadRequestValidator(
            new MediaUploadPolicy(Options.Create(TestUploadSettings.Create())));

        var valid = await validator.ValidateAsync(new InitializeAudioUploadRequest
        {
            OriginalName = "lesson.mp3",
            Extension = ".mp3",
            ContentType = "audio/mpeg",
            Size = 1024
        }, TestContext.Current.CancellationToken);
        var invalid = await validator.ValidateAsync(new InitializeAudioUploadRequest
        {
            OriginalName = "../lesson.mp3",
            Extension = ".txt",
            ContentType = "text/plain",
            Size = 0
        }, TestContext.Current.CancellationToken);
        var controlCharacterName = await validator.ValidateAsync(
            new InitializeAudioUploadRequest
            {
                OriginalName = "lesson\n.mp3",
                Extension = ".mp3",
                ContentType = "audio/mpeg",
                Size = 1024
            },
            TestContext.Current.CancellationToken);

        valid.IsValid.Should().BeTrue();
        invalid.ShouldContain(ErrorCodes.MediaOriginalNameInvalid);
        invalid.ShouldContain(ErrorCodes.MediaExtensionInvalid);
        invalid.ShouldContain(ErrorCodes.MediaExtensionMismatch);
        invalid.ShouldContain(ErrorCodes.MediaContentTypeInvalid);
        invalid.ShouldContain(ErrorCodes.MediaSizeInvalid);
        controlCharacterName.ShouldContain(ErrorCodes.MediaOriginalNameInvalid);
    }

    [Fact]
    public async Task RenameShouldRejectBlankAndControlCharacters()
    {
        var validator = new RenameAudioResourceRequestValidator();

        (await validator.ValidateAsync(
            new RenameAudioResourceRequest { Name = " " },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(
            new RenameAudioResourceRequest { Name = "lesson\n.mp3" },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(
            new RenameAudioResourceRequest { Name = "lesson (2).mp3" },
            TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AdminListShouldValidateAudioResourceStatus()
    {
        var validator = new AdminAudioResourceListRequestValidator();

        (await validator.ValidateAsync(
            new AdminAudioResourceListRequest
            {
                Status = (AudioResourceStatus)999
            },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(
            new AdminAudioResourceListRequest
            {
                Status = AudioResourceStatus.Ready,
                Keyword = "lesson.mp3"
            },
            TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
    }
}
