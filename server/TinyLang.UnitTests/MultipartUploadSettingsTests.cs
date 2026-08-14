using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class MultipartUploadSettingsTests
{
    [Fact]
    public void ValidSettingsShouldPassStartupValidation()
    {
        using var provider = CreateProvider(TestMultipartUploadSettings.Create());

        var action = () => provider
            .GetRequiredService<IOptions<MultipartUploadSettings>>()
            .Value;

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(8, 16, 10000, 60, 300)]
    [InlineData(64, 16, 10, 60, 300)]
    [InlineData(64, 16, 10000, 300, 300)]
    public void InvalidSettingCombinationsShouldFailValidation(
        int thresholdMb,
        int partSizeMb,
        int maxPartCount,
        int cleanupIntervalSeconds,
        int leaseSeconds)
    {
        var settings = TestMultipartUploadSettings.Create() with
        {
            ThresholdMB = thresholdMb,
            PartSizeMB = partSizeMb,
            MaxPartCount = maxPartCount,
            CleanupIntervalSeconds = cleanupIntervalSeconds,
            FinalizationLeaseSeconds = leaseSeconds
        };
        using var provider = CreateProvider(settings);

        var action = () => provider
            .GetRequiredService<IOptions<MultipartUploadSettings>>()
            .Value;

        action.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider CreateProvider(MultipartUploadSettings settings)
    {
        var values = new Dictionary<string, string?>
        {
            [$"{UploadSettings.SectionName}:VideoMaxMB"] = "1024",
            [$"{ObjectStorageSettings.SectionName}:PresignedUrlExpirySeconds"] = "900",
            [$"{MultipartUploadSettings.SectionName}:ThresholdMB"] = settings.ThresholdMB.ToString(),
            [$"{MultipartUploadSettings.SectionName}:PartSizeMB"] = settings.PartSizeMB.ToString(),
            [$"{MultipartUploadSettings.SectionName}:MaxPartCount"] = settings.MaxPartCount.ToString(),
            [$"{MultipartUploadSettings.SectionName}:PartPresignBatchLimit"] = settings.PartPresignBatchLimit.ToString(),
            [$"{MultipartUploadSettings.SectionName}:SessionTtlMinutes"] = settings.SessionTtlMinutes.ToString(),
            [$"{MultipartUploadSettings.SectionName}:CleanupIntervalSeconds"] = settings.CleanupIntervalSeconds.ToString(),
            [$"{MultipartUploadSettings.SectionName}:CleanupBatchSize"] = settings.CleanupBatchSize.ToString(),
            [$"{MultipartUploadSettings.SectionName}:FinalizationLeaseSeconds"] = settings.FinalizationLeaseSeconds.ToString(),
            [$"{MultipartUploadSettings.SectionName}:FinalizationMaxAttempts"] = settings.FinalizationMaxAttempts.ToString(),
            [$"{MultipartUploadSettings.SectionName}:RetryMaxDelayMinutes"] = settings.RetryMaxDelayMinutes.ToString(),
            [$"{MultipartUploadSettings.SectionName}:MaxIncompleteUploadCountPerUser"] = settings.MaxIncompleteUploadCountPerUser.ToString(),
            [$"{MultipartUploadSettings.SectionName}:MaxIncompleteUploadMBPerUser"] = settings.MaxIncompleteUploadMBPerUser.ToString()
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddAppSettings(configuration);
        return services.BuildServiceProvider();
    }
}
