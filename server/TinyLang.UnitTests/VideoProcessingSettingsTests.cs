using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频 heartbeat、租约和临时目录在 options 解析时满足安全约束。
/// </summary>
public sealed class VideoProcessingSettingsTests
{
    /// <summary>
    /// 验证默认视频处理配置通过启动验证。
    /// </summary>
    [Fact]
    public void DefaultVideoSettingsShouldPassValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>());

        var action = () => provider
            .GetRequiredService<IOptions<VideoProcessingSettings>>()
            .Value;

        action.Should().NotThrow();
    }

    /// <summary>
    /// 验证旧的源视频分辨率配置不会再触发固定产品上限校验。
    /// </summary>
    [Fact]
    public void LegacySourceResolutionValuesShouldNotFailValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"{VideoProcessingSettings.SectionName}:MaxSourceWidth"] = "3840",
            [$"{VideoProcessingSettings.SectionName}:MaxSourceHeight"] = "2160"
        });

        var action = () => provider
            .GetRequiredService<IOptions<VideoProcessingSettings>>()
            .Value;

        action.Should().NotThrow();
    }

    /// <summary>
    /// 验证租约至少覆盖三个 heartbeat 窗口的边界配置。
    /// </summary>
    [Fact]
    public void ThreeHeartbeatLeaseWindowShouldPassValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"{VideoProcessingSettings.SectionName}:LeaseSeconds"] = "60",
            [$"{VideoProcessingSettings.SectionName}:HeartbeatIntervalSeconds"] = "20"
        });

        var action = () => provider
            .GetRequiredService<IOptions<VideoProcessingSettings>>()
            .Value;

        action.Should().NotThrow();
    }

    /// <summary>
    /// 验证不足三个 heartbeat 窗口的租约配置被拒绝。
    /// </summary>
    [Fact]
    public void ShortHeartbeatLeaseWindowShouldFailValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"{VideoProcessingSettings.SectionName}:LeaseSeconds"] = "60",
            [$"{VideoProcessingSettings.SectionName}:HeartbeatIntervalSeconds"] = "21"
        });

        var action = () => provider
            .GetRequiredService<IOptions<VideoProcessingSettings>>()
            .Value;

        action.Should().Throw<OptionsValidationException>();
    }

    /// <summary>
    /// 验证当前平台的文件系统根目录不能作为视频处理临时目录。
    /// </summary>
    [Fact]
    public void FilesystemRootTemporaryDirectoryShouldFailValidation()
    {
        var root = Path.GetPathRoot(Path.GetFullPath("."));
        root.Should().NotBeNullOrWhiteSpace();
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"{VideoProcessingSettings.SectionName}:TemporaryDirectory"] = root
        });

        var action = () => provider
            .GetRequiredService<IOptions<VideoProcessingSettings>>()
            .Value;

        action.Should().Throw<OptionsValidationException>();
    }

    /// <summary>
    /// 使用内存配置注册完整 options 管线并返回测试 provider。
    /// </summary>
    private static ServiceProvider CreateProvider(
        IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddAppSettings(configuration);
        return services.BuildServiceProvider();
    }
}
