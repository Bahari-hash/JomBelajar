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

/// <summary>
/// 提供认证、缓存、消息、邮件、安全和对象存储基础设施的注册入口。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 JWT bearer authentication、令牌签发服务和令牌撤销检查。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configuration">JWT 配置源。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITokenBlacklist, TokenBlacklist>();

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
                    var tokenId = context.Principal?.FindFirst(JwtClaimNamesExtension.TokenId)?.Value;
                    if (string.IsNullOrWhiteSpace(tokenId))
                    {
                        context.Fail("Missing token id");
                        return;
                    }

                    var blacklist = context.HttpContext.RequestServices.GetRequiredService<ITokenBlacklist>();
                    if (await blacklist.ContainsAccessTokenAsync(tokenId, context.HttpContext.RequestAborted))
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

    /// <summary>
    /// 注册基于最低用户角色的授权 handler 和策略。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
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

    /// <summary>
    /// 创建并注册共享 Redis 连接复用器。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configuration">Redis 连接配置源。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddCacheService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("RedisConnection") ??
            throw new InvalidOperationException("Cannot get redis connection string");
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnection));

        return services;
    }

    /// <summary>
    /// 配置 MassTransit、RabbitMQ、邮件与视频 consumer 和消息重试策略。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configuration">RabbitMQ 配置源。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddMessageQueueService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMqSettings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ??
            throw new InvalidOperationException("Cannot get rabbitmq settings from configuration");

        services.AddMassTransit(options =>
        {
            // Register background worker here
            options.AddConsumer<EmailSendingWorker>();
            options.AddConsumer<VideoProcessingWorker, VideoProcessingWorkerDefinition>();

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

    /// <summary>
    /// 注册模板内容 provider 和 Scriban 渲染器。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddTemplatesRenderingService(this IServiceCollection services)
    {
        services.AddSingleton<ITemplateContentProvider, TemplateContentProvider>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();

        return services;
    }

    /// <summary>
    /// 注册邮件发送调度器和 SMTP provider。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddEmailSendingService(this IServiceCollection services)
    {
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<IEmailProvider, MailKitProvider>();

        return services;
    }

    /// <summary>
    /// 注册 secret hashing、数据库异常分类和 HTML 清理服务。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddSecureService(this IServiceCollection services)
    {
        services.AddSingleton<ISecretHasher, SecretHasher>();
        services.AddSingleton<IDatabaseExceptionClassifier, PostgresDatabaseExceptionClassifier>();
        services.AddSingleton<IHtmlContentSanitizer, HtmlContentSanitizer>();

        return services;
    }

    /// <summary>
    /// 注册验证码生成、Redis 存储和发送编排服务。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddVerificationCodeService(this IServiceCollection services)
    {
        services.AddSingleton<IVerificationCodeGenerator, VerificationCodeGenerator>();
        services.AddSingleton<IVerificationCodeStore, VerificationCodeStore>();
        services.AddScoped<IVerificationCodeSender, VerificationCodeSender>();

        return services;
    }

    /// <summary>
    /// 根据配置创建 S3-compatible client 并注册对象存储服务。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
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

    /// <summary>
    /// 注册 Web API 使用的视频 delivery 和 PostgreSQL 原子进度实现。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddVideoApplicationInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IVideoDeliveryUrlService, VideoDeliveryUrlService>();
        services.AddScoped<IUserVideoProgressStore, PostgresUserVideoProgressStore>();
        return services;
    }

    /// <summary>
    /// 注册 Web API 视频 consumer 使用的消息发布、进程、探测、转码和启动检查实现。
    /// </summary>
    /// <param name="services">worker 服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddVideoProcessingInfrastructure(
        this IServiceCollection services)
    {
        services.AddScoped<IVideoProcessingQueue, MassTransitVideoProcessingQueue>();
        services.AddSingleton<IMediaProcessRunner, MediaProcessRunner>();
        services.AddSingleton<IMediaProbe, FfprobeMediaProbe>();
        services.AddSingleton<IVideoTranscoder, FfmpegVideoTranscoder>();
        services.AddSingleton<IVideoToolPreflight, VideoToolPreflight>();
        services.AddHostedService<VideoToolPreflightWorker>();
        return services;
    }
}
