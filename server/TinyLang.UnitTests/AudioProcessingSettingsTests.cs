using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频输出产品参数和 worker 组合关系在 options 解析时失败得足够早。
/// </summary>
public sealed class AudioProcessingSettingsTests
{
    /// <summary>
    /// 验证音频默认配置通过启动验证。
    /// </summary>
    [Fact]
    public void DefaultAudioSettingsShouldPassValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>());

        var action = () => provider
            .GetRequiredService<IOptions<AudioProcessingSettings>>()
            .Value;

        action.Should().NotThrow();
    }

    /// <summary>
    /// 验证偏离固定 MP3 profile 的配置被拒绝。
    /// </summary>
    [Fact]
    public void NonProductOutputProfileShouldFailValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"{AudioProcessingSettings.SectionName}:OutputSampleRate"] = "48000"
        });

        var action = () => provider
            .GetRequiredService<IOptions<AudioProcessingSettings>>()
            .Value;

        action.Should().Throw<OptionsValidationException>();
    }

    /// <summary>
    /// 验证无法覆盖进程超时余量的租约配置被拒绝。
    /// </summary>
    [Fact]
    public void ShortLeaseShouldFailValidation()
    {
        using var provider = CreateProvider(new Dictionary<string, string?>
        {
            [$"{AudioProcessingSettings.SectionName}:LeaseSeconds"] = "1800"
        });

        var action = () => provider
            .GetRequiredService<IOptions<AudioProcessingSettings>>()
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
