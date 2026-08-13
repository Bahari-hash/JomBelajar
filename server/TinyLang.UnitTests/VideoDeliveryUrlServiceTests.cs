using FluentAssertions;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频和音频媒体使用 OSS 公共直链交付，以及对象路径边界校验。
/// </summary>
public sealed class VideoDeliveryUrlServiceTests
{
    [Fact]
    public void DirectObjectStorageShouldUsePermanentObjectStorageUrl()
    {
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.GetPublicUrl("videos/id/poster.jpg"))
            .Returns("https://oss.example.com/media/videos/id/poster.jpg");
        var service = new VideoDeliveryUrlService(storage.Object);

        var result = service.CreateUrl(
            "videos/id/poster.jpg",
            "videos/id/");

        result.Url.Should().Be("https://oss.example.com/media/videos/id/poster.jpg");
        result.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ObjectOutsideProtectedPrefixShouldBeRejected()
    {
        var service = new VideoDeliveryUrlService(Mock.Of<IObjectStorageService>());

        var action = () => service.CreateUrl(
            "videos/other/master.m3u8",
            "videos/expected/");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AudioObjectPrefixShouldUseTheSameDirectStorageContract()
    {
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.GetPublicUrl(
                "audios/id/outputs/version/audio.mp3"))
            .Returns("https://oss.example.com/media/audios/id/outputs/version/audio.mp3");
        var service = new VideoDeliveryUrlService(storage.Object);

        var result = service.CreateUrl(
            "audios/id/outputs/version/audio.mp3",
            "audios/id/outputs/version/");

        result.Url.Should().Be(
            "https://oss.example.com/media/audios/id/outputs/version/audio.mp3");
        result.ExpiresAt.Should().BeNull();
    }
}
