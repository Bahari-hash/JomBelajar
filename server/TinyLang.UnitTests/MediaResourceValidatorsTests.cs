using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Policies;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class MediaResourceValidatorsTests
{
    [Fact]
    public async Task AvatarShouldUseConfiguredPictureRules()
    {
        var validator = new AvatarPresignRequestValidator(CreatePolicy());
        var request = new AvatarPresignRequest
        {
            OriginalName = "avatar.webp",
            Extension = ".webp",
            ContentType = "image/webp",
            Size = Megabytes(5)
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AvatarShouldRejectContentTypeThatDoesNotMatchExtension()
    {
        var validator = new AvatarPresignRequestValidator(CreatePolicy());
        var request = new AvatarPresignRequest
        {
            OriginalName = "avatar.png",
            Extension = ".png",
            ContentType = "text/html",
            Size = 1024
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.ShouldContain(ErrorCodes.MediaContentTypeInvalid);
    }

    [Fact]
    public async Task AvatarShouldRejectMissingMetadataAndNonPositiveSize()
    {
        var validator = new AvatarPresignRequestValidator(CreatePolicy());
        var request = new AvatarPresignRequest
        {
            OriginalName = string.Empty,
            Extension = string.Empty,
            ContentType = string.Empty,
            Size = 0
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.ShouldContain(ErrorCodes.MediaOriginalNameRequired);
        result.ShouldContain(ErrorCodes.MediaExtensionRequired);
        result.ShouldContain(ErrorCodes.MediaContentTypeRequired);
        result.ShouldContain(ErrorCodes.MediaSizeInvalid);
    }

    [Theory]
    [InlineData(ResourceModule.ArticlePicture, "picture.gif", ".gif", "image/gif", 5)]
    [InlineData(ResourceModule.Audio, "lesson.webm", ".webm", "audio/webm", 20)]
    [InlineData(ResourceModule.CourseVideo, "course.webm", ".webm", "video/webm", 63)]
    public async Task AdminMediaShouldApplyRulesForEachModule(
        ResourceModule module,
        string originalName,
        string extension,
        string contentType,
        int sizeMb)
    {
        var validator = CreateAdminPresignValidator();
        var request = CreateAdminRequest(
            module,
            originalName,
            extension,
            contentType,
            Megabytes(sizeMb));

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AdminMediaShouldRejectWrongModuleTypeAndSizeOverflow()
    {
        var validator = CreateAdminPresignValidator();
        var wrongType = await validator.ValidateAsync(
            CreateAdminRequest(
                ResourceModule.CourseVideo,
                "course.png",
                ".png",
                "image/png",
                1024),
            TestContext.Current.CancellationToken);
        var oversizedAudio = await validator.ValidateAsync(
            CreateAdminRequest(
                ResourceModule.Audio,
                "lesson.mp3",
                ".mp3",
                "audio/mpeg",
                Megabytes(20) + 1),
            TestContext.Current.CancellationToken);

        wrongType.ShouldContain(ErrorCodes.MediaExtensionInvalid);
        oversizedAudio.ShouldContain(ErrorCodes.MediaSizeLimitExceeded);
    }

    private static AdminMediaPresignRequest CreateAdminRequest(
        ResourceModule module,
        string originalName,
        string extension,
        string contentType,
        long size)
        => new()
        {
            Module = module,
            OriginalName = originalName,
            Extension = extension,
            ContentType = contentType,
            Size = size
        };

    private static MediaUploadPolicy CreatePolicy()
        => new(Options.Create(TestUploadSettings.Create()));

    private static AdminMediaPresignRequestValidator CreateAdminPresignValidator()
        => new(CreatePolicy(), Options.Create(TestMultipartUploadSettings.Create()));

    private static long Megabytes(int value) => (long)value * 1024 * 1024;

    [Theory]
    [InlineData(63, false)]
    [InlineData(64, true)]
    public async Task MultipartRequestShouldEnforceConfiguredThreshold(
        int sizeMb,
        bool expectedValid)
    {
        var validator = new MultipartUploadRequestValidator(
            CreatePolicy(),
            Options.Create(TestMultipartUploadSettings.Create()));
        var request = new MultipartUploadRequest
        {
            Module = ResourceModule.CourseVideo,
            OriginalName = "course.mp4",
            Extension = ".mp4",
            ContentType = "video/mp4",
            Size = Megabytes(sizeMb)
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public async Task SimplePresignShouldStopBeforeMultipartThreshold(
        int sizeMb,
        bool expectedValid)
    {
        var validator = CreateAdminPresignValidator();
        var request = CreateAdminRequest(
            ResourceModule.CourseVideo,
            "course.mp4",
            ".mp4",
            "video/mp4",
            Megabytes(sizeMb));

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(ResourceModule.Avatar)]
    [InlineData((ResourceModule)999)]
    public async Task CapabilityShouldRejectNonAdminOrUndefinedModule(ResourceModule module)
    {
        var validator = new AdminMediaCapabilityRequestValidator(CreatePolicy());

        var result = await validator.ValidateAsync(
            new AdminMediaCapabilityRequest { Module = module },
            TestContext.Current.CancellationToken);

        result.ShouldContain(ErrorCodes.ResourceModuleInvalid);
    }

    [Fact]
    public async Task PartPresignShouldRejectDuplicateNumbers()
    {
        var validator = new MultipartPartPresignRequestValidator(
            Options.Create(TestMultipartUploadSettings.Create()));

        var result = await validator.ValidateAsync(
            new MultipartPartPresignRequest { PartNumbers = [1, 1] },
            TestContext.Current.CancellationToken);

        result.ShouldContain(ErrorCodes.MultipartUploadPartsInvalid);
    }

    [Fact]
    public async Task CompleteShouldRejectDuplicatePartsAndControlCharacters()
    {
        var validator = new CompleteMultipartUploadRequestValidator(
            Options.Create(TestMultipartUploadSettings.Create()));
        var request = new CompleteMultipartUploadRequest
        {
            Parts =
            [
                new CompletedMultipartPartRequest(1, "etag-1"),
                new CompletedMultipartPartRequest(1, "etag\n")
            ]
        };

        var result = await validator.ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.ShouldContain(ErrorCodes.MultipartUploadPartsInvalid);
    }
}

internal static class ValidationResultAssertions
{
    public static void ShouldContain(this ValidationResult result, ErrorCodes errorCode)
        => result.Errors.Should().Contain(error => error.ErrorCode == errorCode.ToString());
}

internal static class TestUploadSettings
{
    public static UploadSettings Create() => new()
    {
        PictureMaxMB = 5,
        AudioMaxMB = 20,
        VideoMaxMB = 1024,
        PictureAllowedTypes = new Dictionary<string, string[]>
        {
            [".png"] = ["image/png"],
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"],
            [".gif"] = ["image/gif"],
            [".webp"] = ["image/webp"]
        },
        AudioAllowedTypes = new Dictionary<string, string[]>
        {
            [".mp3"] = ["audio/mpeg"],
            [".wav"] = ["audio/wav", "audio/x-wav"],
            [".webm"] = ["audio/webm"],
            [".ogg"] = ["audio/ogg"]
        },
        VideoAllowedTypes = new Dictionary<string, string[]>
        {
            [".mp4"] = ["video/mp4"],
            [".webm"] = ["video/webm"],
            [".ogg"] = ["video/ogg"]
        }
    };
}
