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

        return services;
    }
}
