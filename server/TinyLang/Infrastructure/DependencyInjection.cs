using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
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
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var token = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        context.Fail("Missing bearer token");
                        return;
                    }

                    var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                    if (await blacklist.ContainsAsync(token, context.HttpContext.RequestAborted))
                    {
                        context.Fail("Token has been revoked");
                        return;
                    }

                    var userIdValue = context.Principal?.FindFirst(JwtClaimNamesExtension.UserId)?.Value;
                    var versionValue = context.Principal?.FindFirst(JwtClaimNamesExtension.TokenVersion)?.Value;
                    if (!Guid.TryParse(userIdValue, out var userId) || !int.TryParse(versionValue, out var tokenVersion))
                    {
                        context.Fail("Invalid token claims");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();
                    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, context.HttpContext.RequestAborted);
                    if (user is null || user.IsDeleted || user.IsBanned || user.TokenVersion != tokenVersion)
                    {
                        context.Fail("User is no longer authorized");
                    }
                }
            };
        });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicy(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireUser, policy =>
            {
                var minimumUser = new MinimumRoleRequirement(UserRole.User);
                policy.Requirements.Add(minimumUser);
            })
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
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = CacheKeys.RedisInstanceName;
        });
        services.AddSingleton<ITokenBlacklist, TokenBlacklist>();

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
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IDatabaseExceptionClassifier, PostgresDatabaseExceptionClassifier>();

        return services;
    }

    public static IServiceCollection AddVerificationCodeService(this IServiceCollection services)
    {
        services.AddSingleton<IVerificationCodeGenerator, VerificationCodeGenerator>();
        services.AddSingleton<IVerificationCodeStore, VerificationCodeStore>();
        services.AddScoped<IVerificationCodeSender, VerificationCodeSender>();

        return services;
    }

    public static IServiceCollection AddObjectStorageService(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3>(serviceProvider =>
        {
            var settings = serviceProvider
                .GetRequiredService<IOptions<ObjectStorageSettings>>()
                .Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = settings.ForcePathStyle
            };
            if (string.IsNullOrWhiteSpace(settings.ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
            }
            else
            {
                config.ServiceURL = settings.ServiceUrl;
                config.AuthenticationRegion = settings.Region;
            }

            if (string.IsNullOrWhiteSpace(settings.AccessKey))
            {
                return new AmazonS3Client(config);
            }

            var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
            return new AmazonS3Client(credentials, config);
        });
        services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();

        return services;
    }
}
