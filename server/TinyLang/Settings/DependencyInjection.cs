using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace TinyLang.Settings;

public static class DependencyInjection
{
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
