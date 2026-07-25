using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TinyLang.Constants;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

public sealed class VerificationCodeStore(
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
        => CacheKeys.BuildRedisKey($"verification_code:v2:{email.Trim()}:{purpose}");

    public async Task<bool> TryConsumeAsync(
        string email,
        VerificationCodePurpose purpose,
        string expectedValue,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
                ConsumeScript,
                [new RedisKey(BuildKey(email, purpose))],
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
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        await database.StringSetAsync(
                BuildKey(email, purpose),
                code,
                TimeSpan.FromMinutes(_settings.ExpMinutes))
            .WaitAsync(cancellationToken);
    }

    public async Task<string?> GetAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        var value = await database.StringGetAsync(BuildKey(email, purpose))
            .WaitAsync(cancellationToken);
        return value.IsNull ? null : value.ToString();
    }
}
