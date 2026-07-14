using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TinyLang.Constants;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Settings;
using TinyLang.Workers;

namespace TinyLang.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ??
                throw new InvalidOperationException("Cannot get jwt settings from configuration");

            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.JwtSecret)),

                // Specify the name and role claim types
                // in order to make authorization service working
                NameClaimType = JwtClaimNamesExtension.Name,
                RoleClaimType = JwtClaimNamesExtension.Role,
            };
        });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicy(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
            {
                var minimumAdmin = new MinimumRoleRequirement(UserRole.Admin);
                policy.Requirements.Add(minimumAdmin);
            })
            .AddPolicy(AuthorizationPolicies.RequireEditor, policy =>
            {
                var minimumEditor = new MinimumRoleRequirement(UserRole.Editor);
                policy.Requirements.Add(minimumEditor);
            });

        return services;
    }

    public static IServiceCollection AddCacheService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("RedisConnection") ??
            throw new InvalidOperationException("Cannot get redis connection string");
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "tiny-lang";
        });

        return services;
    }

    public static IServiceCollection AddMessageQueueService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ??
            throw new InvalidOperationException("Cannot get rabbitmq settings from configuration");

        services.AddMassTransit(options =>
        {
            // Register background worker here
            options.AddConsumer<EmailSendingWorker>();

            options.AddConfigureEndpointsCallback((_, config) =>
                config.UseMessageRetry(retry => retry.Incremental(
                    retryLimit: 5,
                    initialInterval: TimeSpan.FromSeconds(2),
                    intervalIncrement: TimeSpan.FromSeconds(5))));

            options.UsingRabbitMq((context, config) =>
            {
                config.Host(
                    rabbitMqSettings.Host,
                    (ushort)rabbitMqSettings.Port,
                    rabbitMqSettings.VirtualHost, h =>
                {
                    h.Username(rabbitMqSettings.Username);
                    h.Password(rabbitMqSettings.Password);
                });

                config.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    public static IServiceCollection AddTemplatesRenderingService(this IServiceCollection services)
    {
        services.AddSingleton<ITemplateContentProvider, TemplateContentProvider>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();

        return services;
    }

    public static IServiceCollection AddEmailSendingService(this IServiceCollection services)
    {
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IEmailProvider, MailKitProvider>();

        return services;
    }

    public static IServiceCollection AddSecureService(this IServiceCollection services)
    {
        services.AddSingleton<ISecretHasher, SecretHasher>();

        return services;
    }

    public static IServiceCollection AddVerificationCodeService(this IServiceCollection services)
    {
        services.AddSingleton<IVerificationCodeGenerator, VerificationCodeGenerator>();

        return services;
    }
}
