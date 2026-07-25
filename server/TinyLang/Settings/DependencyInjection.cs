using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TinyLang.Settings;

/// <summary>
/// 提供强类型应用配置的依赖注入注册入口。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 绑定并校验应用配置，并配置可信代理的 forwarded headers 选项。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configuration">应用配置源。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    /// <exception cref="InvalidOperationException">启用转发头但可信代理配置缺失或无效。</exception>
    public static IServiceCollection AddAppSettings(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName);
        services.AddOptions<JwtSettings>()
            .Bind(jwtSettings)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var emailSettings = configuration.GetSection(EmailSettings.SectionName);
        services.AddOptions<EmailSettings>()
            .Bind(emailSettings)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var mqSettings = configuration.GetSection(RabbitMqSettings.SectionName);
        services.AddOptions<RabbitMqSettings>()
            .Bind(mqSettings)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var vcodeSettings = configuration.GetSection(VerificationCodeSettings.SectionName);
        services.AddOptions<VerificationCodeSettings>()
            .Bind(vcodeSettings)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var uploadSettings = configuration.GetSection(UploadSettings.SectionName);
        services.AddOptions<UploadSettings>()
            .Bind(uploadSettings)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var multipartUploadSettings = configuration.GetSection(
            MultipartUploadSettings.SectionName);
        var configuredVideoMaxMb = configuration.GetValue<int?>(
            $"{UploadSettings.SectionName}:VideoMaxMB");
        var configuredPresignExpirySeconds = configuration.GetValue<int?>(
            $"{ObjectStorageSettings.SectionName}:PresignedUrlExpirySeconds");
        services.AddOptions<MultipartUploadSettings>()
            .Bind(multipartUploadSettings)
            .ValidateDataAnnotations()
            .Validate(
                settings => settings.ThresholdMB >= settings.PartSizeMB,
                "Multipart upload threshold must be greater than or equal to the part size.")
            .Validate(
                settings => configuredVideoMaxMb is null ||
                    ((long)settings.PartSizeMB * settings.MaxPartCount >= configuredVideoMaxMb &&
                        settings.ThresholdMB <= configuredVideoMaxMb &&
                        settings.MaxIncompleteUploadMBPerUser >= configuredVideoMaxMb),
                "Multipart settings must support the configured maximum course video size.")
            .Validate(
                settings => configuredPresignExpirySeconds is null ||
                    configuredPresignExpirySeconds <= (long)settings.SessionTtlMinutes * 60,
                "Presigned URL expiry must not exceed the multipart session TTL.")
            .Validate(
                settings => settings.FinalizationLeaseSeconds > settings.CleanupIntervalSeconds,
                "Finalization lease must be longer than the cleanup interval.")
            .ValidateOnStart();

        var rateLimitSettings = configuration.GetSection(RateLimitSettings.SectionName);
        services.AddOptions<RateLimitSettings>()
            .Bind(rateLimitSettings)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var objectStorageSettings = configuration.GetSection(ObjectStorageSettings.SectionName);
        services.AddOptions<ObjectStorageSettings>()
            .Bind(objectStorageSettings)
            .ValidateDataAnnotations()
            .Validate(
                settings => string.IsNullOrWhiteSpace(settings.AccessKey) ==
                    string.IsNullOrWhiteSpace(settings.SecretKey),
                "Object storage AccessKey and SecretKey must be configured together.")
            .ValidateOnStart();

        var forwardedHeadersSettings = configuration
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();
        if (forwardedHeadersSettings.Enabled && (forwardedHeadersSettings.KnownProxies?.Length ?? 0) == 0)
        {
            throw new InvalidOperationException(
                "Forwarded headers require at least one configured known proxy.");
        }
        services.AddSingleton(forwardedHeadersSettings);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            if (!forwardedHeadersSettings.Enabled)
            {
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var proxy in forwardedHeadersSettings.KnownProxies ?? [])
            {
                if (!IPAddress.TryParse(proxy, out var address))
                {
                    throw new InvalidOperationException(
                        $"Invalid forwarded headers proxy address: {proxy}");
                }

                options.KnownProxies.Add(address);
            }
        });

        return services;
    }
}
