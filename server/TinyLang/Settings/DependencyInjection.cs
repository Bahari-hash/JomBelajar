using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
