using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TinyLang.Constants;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

public sealed class VerificationCodeStore(
    IDistributedCache cacheService,
    IConnectionMultiplexer redisConnection,
    IOptions<VerificationCodeSettings> options) : IVerificationCodeStore
{
    private const string ConsumeScript = """
        local current = redis.call('GET', KEYS[1])
        if current == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private readonly VerificationCodeSettings _settings = options.Value;

    private static string BuildKey(string email, VerificationCodePurpose purpose)
        => $"verification_code:{email.Trim()}:{purpose}";

    public async Task<bool> TryConsumeAsync(
        string email,
        VerificationCodePurpose purpose,
        string expectedValue,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        var physicalKey = CacheKeys.RedisInstanceName + BuildKey(email, purpose);
        var result = await database.ScriptEvaluateAsync(
                ConsumeScript,
                [new RedisKey(physicalKey)],
                [new RedisValue(expectedValue)])
            .WaitAsync(cancellationToken);
        return (long)result == 1;
    }

    public async Task SaveAsync(
        string email,
        VerificationCodePurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = utcNow.AddMinutes(_settings.ExpMinutes)
        };

        var key = BuildKey(email, purpose);
        await cacheService.SetStringAsync(key, code, options, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var value = await cacheService.GetStringAsync(BuildKey(email, purpose), cancellationToken);
        return value is not null;
    }

    public async Task<string?> GetAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        return await cacheService.GetStringAsync(BuildKey(email, purpose), cancellationToken);
    }
}
