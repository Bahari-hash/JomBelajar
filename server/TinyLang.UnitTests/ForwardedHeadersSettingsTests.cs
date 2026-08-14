using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class ForwardedHeadersSettingsTests
{
    [Fact]
    public void ProductionShouldRequireForwardedHeaders()
    {
        var configuration = CreateConfiguration(false, []);
        var environment = CreateEnvironment(Environments.Production);
        var services = new ServiceCollection();

        var action = () => services.AddAppSettings(configuration, environment);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Forwarded headers must be enabled*");
    }

    [Fact]
    public void EnabledForwardedHeadersShouldRequireTrustedProxyOrNetwork()
    {
        var configuration = CreateConfiguration(true, []);
        var services = new ServiceCollection();

        var action = () => services.AddAppSettings(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*known proxy or network*");
    }

    [Fact]
    public void ValidTrustedNetworkShouldRegisterForwardedHeadersSettings()
    {
        var configuration = CreateConfiguration(true, ["172.16.0.0/12"]);
        var services = new ServiceCollection();
        services.AddAppSettings(configuration);
        using var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<ForwardedHeadersSettings>();

        settings.Enabled.Should().BeTrue();
        settings.KnownNetworks.Should().Equal("172.16.0.0/12");
    }

    [Fact]
    public void InvalidTrustedNetworkShouldFailConfiguration()
    {
        var configuration = CreateConfiguration(true, ["not-a-network"]);
        var services = new ServiceCollection();

        var action = () => services.AddAppSettings(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid forwarded headers proxy network*");
    }

    private static IConfiguration CreateConfiguration(
        bool enabled,
        string[] networks)
    {
        var values = new Dictionary<string, string?>
        {
            ["ForwardedHeadersSettings:Enabled"] = enabled.ToString()
        };
        for (var index = 0; index < networks.Length; index++)
        {
            values[$"ForwardedHeadersSettings:KnownNetworks:{index}"] = networks[index];
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }
}
