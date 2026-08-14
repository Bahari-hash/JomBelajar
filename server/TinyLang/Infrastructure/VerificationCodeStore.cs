using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TinyLang.Constants;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 Redis 有效期和 Lua compare-and-delete 实现验证码存储。
/// </summary>
/// <param name="redisConnection">Redis 连接复用器。</param>
/// <param name="options">验证码有效期配置。</param>
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

    private const string IncrementFailureScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    private readonly VerificationCodeSettings _settings = options.Value;

    /// <summary>
    /// 构建隔离邮箱和用途的验证码 Redis 键。
    /// </summary>
    /// <param name="email">验证码所属邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <returns>带应用命名空间的 Redis 键。</returns>
    private static string BuildKey(string email, VerificationCodePurpose purpose)
        => CacheKeys.BuildRedisKey($"verification_code:v2:{email.Trim()}:{purpose}");

    private static string BuildFailureKey(string email, VerificationCodePurpose purpose)
        => CacheKeys.BuildRedisKey($"verification_code_failures:v2:{email.Trim()}:{purpose}");

    /// <inheritdoc />
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

    /// <inheritdoc />
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
        await database.KeyDeleteAsync(BuildFailureKey(email, purpose))
            .WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<int> IncrementFailureAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        var result = await database.ScriptEvaluateAsync(
                IncrementFailureScript,
                [new RedisKey(BuildFailureKey(email, purpose))],
                [new RedisValue((_settings.ExpMinutes * 60).ToString())])
            .WaitAsync(cancellationToken);
        return (int)(long)result;
    }

    /// <inheritdoc />
    public async Task ResetFailuresAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        await database.KeyDeleteAsync(BuildFailureKey(email, purpose))
            .WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var database = redisConnection.GetDatabase();
        await database.KeyDeleteAsync(BuildKey(email, purpose))
            .WaitAsync(cancellationToken);
    }
}
