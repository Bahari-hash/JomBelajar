using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyLang.Constants;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

public static class RateLimitingDependencyInjection
{
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

    private static string GetStrictCodePartitionKey(HttpContext context)
        => TryGetUserId(context, out var userId)
            ? $"user:{userId:N}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static string GetUploadPresignPartitionKey(HttpContext context)
        => TryGetUserId(context, out var userId)
            ? $"user:{userId:N}"
            : $"unauthenticated:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static bool TryGetUserId(HttpContext context, out Guid userId)
        => Guid.TryParse(
            context.User.FindFirst(JwtClaimNamesExtension.UserId)?.Value,
            out userId);

    private static int GetRetryAfterSeconds(RateLimitLease lease, int fallbackSeconds)
        => lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : fallbackSeconds;
}
