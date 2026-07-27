using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频 delivery HMAC 固定向量、路径边界和开发直连。
/// </summary>
public sealed class VideoDeliveryUrlServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SignedCdnShouldMatchFixedHmacVector()
    {
        var service = new VideoDeliveryUrlService(
            Mock.Of<IObjectStorageService>(),
            Options.Create(new VideoDeliverySettings
            {
                Mode = "SignedCdn",
                CdnBaseUrl = "https://media.example.com",
                KeyId = "key-2026-01",
                SigningSecret = "0123456789abcdef0123456789abcdef",
                TokenTtlSeconds = 300
            }),
            new TestTimeProvider(Now));
        const string prefix =
            "videos/11111111111111111111111111111111/outputs/22222222222222222222222222222222/";

        var result = service.CreateUrl($"{prefix}master.m3u8", prefix);

        result.ExpiresAt.Should().Be(Now.AddMinutes(5));
        result.Url.Should().Be(
            "https://media.example.com/auth/key-2026-01/" +
            "Qt7Fs2T5qh_bhEU4TJBAfohzfu3f9JZd6q_IcclOxH8/1785153900/" +
            $"{prefix}master.m3u8");
    }

    [Fact]
    public void ObjectOutsideProtectedPrefixShouldBeRejected()
    {
        var service = new VideoDeliveryUrlService(
            Mock.Of<IObjectStorageService>(),
            Options.Create(new VideoDeliverySettings()),
            new TestTimeProvider(Now));

        var action = () => service.CreateUrl(
            "videos/other/master.m3u8",
            "videos/expected/");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DirectDevelopmentShouldUseObjectStorageUrl()
    {
        var storage = new Mock<IObjectStorageService>();
        storage.Setup(value => value.GetPublicUrl("videos/id/poster.jpg"))
            .Returns("https://localhost/media/videos/id/poster.jpg");
        var service = new VideoDeliveryUrlService(
            storage.Object,
            Options.Create(new VideoDeliverySettings()),
            new TestTimeProvider(Now));

        var result = service.CreateUrl(
            "videos/id/poster.jpg",
            "videos/id/");

        result.Url.Should().Be("https://localhost/media/videos/id/poster.jpg");
    }
}
