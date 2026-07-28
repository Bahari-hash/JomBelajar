using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyLang.Constants;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 提供 endpoint policy 和全局 fallback limiter 的注册与分区规则。
/// </summary>
public static class RateLimitingDependencyInjection
{
    /// <summary>
    /// 注册验证码、上传、音视频播放/进度和全局并发限流策略。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configuration">限流配置源。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    /// <exception cref="InvalidOperationException">无法读取限流配置。</exception>
    public static IServiceCollection AddCustomRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(RateLimitSettings.SectionName)
            .Get<RateLimitSettings>() ??
            throw new InvalidOperationException("Cannot get rate limit settings from configuration");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfterSeconds = GetRetryAfterSeconds(
                    context.Lease,
                    settings.StrictCodeWindowSeconds);
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.ContentType = "application/json";
                response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                await response.WriteAsJsonAsync(
                    new
                    {
                        code = StatusCodes.Status429TooManyRequests,
                        message = "请求过于频繁，请稍后再试",
                        retryAfter = retryAfterSeconds
                    },
                    cancellationToken);
            };

            options.AddPolicy(RateLimitPolicies.StrictCodeLimit, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetStrictCodePartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.StrictCodePermitLimit,
                        Window = TimeSpan.FromSeconds(settings.StrictCodeWindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.UploadPresignLimit, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetUploadPresignPartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.UploadPresignTokenLimit,
                        TokensPerPeriod = settings.UploadPresignTokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(
                            settings.UploadPresignReplenishmentPeriodSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.UploadCommandLimit, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetUploadPresignPartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.UploadCommandTokenLimit,
                        TokensPerPeriod = settings.UploadCommandTokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(
                            settings.UploadCommandReplenishmentPeriodSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.VideoPlaybackLimit, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetUploadPresignPartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.VideoPlaybackTokenLimit,
                        TokensPerPeriod = settings.VideoPlaybackTokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(
                            settings.VideoPlaybackReplenishmentPeriodSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.VideoProgressLimit, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetUploadPresignPartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.VideoProgressTokenLimit,
                        TokensPerPeriod = settings.VideoProgressTokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(
                            settings.VideoProgressReplenishmentPeriodSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitPolicies.AudioPlaybackLimit, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    GetUploadPresignPartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = settings.AudioPlaybackTokenLimit,
                        TokensPerPeriod = settings.AudioPlaybackTokensPerPeriod,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(
                            settings.AudioPlaybackReplenishmentPeriodSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => context.GetEndpoint()?.Metadata
                    .GetMetadata<EnableRateLimitingAttribute>() is not null
                    ? RateLimitPartition.GetNoLimiter("endpoint-policy")
                    : RateLimitPartition.GetConcurrencyLimiter(
                        "global",
                        _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = settings.GlobalFallbackPermitLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = settings.GlobalFallbackQueueLimit
                        }));
        });

        return services;
    }

    /// <summary>
    /// 优先按用户标识、否则按远端 IP 构建验证码限流分区键。
    /// </summary>
    /// <param name="context">当前 HTTP 上下文。</param>
    /// <returns>验证码限流分区键。</returns>
    private static string GetStrictCodePartitionKey(HttpContext context)
        => TryGetUserId(context, out var userId)
            ? $"user:{userId:N}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    /// <summary>
    /// 优先按用户标识、否则按未认证远端 IP 构建上传限流分区键。
    /// </summary>
    /// <param name="context">当前 HTTP 上下文。</param>
    /// <returns>上传预签名限流分区键。</returns>
    private static string GetUploadPresignPartitionKey(HttpContext context)
        => TryGetUserId(context, out var userId)
            ? $"user:{userId:N}"
            : $"unauthenticated:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    /// <summary>
    /// 尝试从当前 principal 的 user ID claim 解析用户标识。
    /// </summary>
    /// <param name="context">当前 HTTP 上下文。</param>
    /// <param name="userId">成功时接收解析后的用户标识。</param>
    /// <returns>claim 存在且为有效 GUID 时返回 <see langword="true"/>。</returns>
    private static bool TryGetUserId(HttpContext context, out Guid userId)
        => Guid.TryParse(
            context.User.FindFirst(JwtClaimNamesExtension.UserId)?.Value,
            out userId);

    /// <summary>
    /// 从拒绝 lease 读取 Retry-After，并在元数据缺失时使用配置回退值。
    /// </summary>
    /// <param name="lease">被拒绝的限流 lease。</param>
    /// <param name="fallbackSeconds">缺少元数据时使用的秒数。</param>
    /// <returns>至少为一秒的重试等待时间。</returns>
    private static int GetRetryAfterSeconds(RateLimitLease lease, int fallbackSeconds)
        => lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : fallbackSeconds;
}
