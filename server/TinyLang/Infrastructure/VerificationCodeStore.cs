using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

public sealed class VerificationCodeStore(
    IDistributedCache cacheService, IOptions<VerificationCodeSettings> options) : IVerificationCodeStore
{
    private readonly VerificationCodeSettings _settings = options.Value;

    private static string BuildKey(string email, VerificationCodePurpose purpose)
        => $"verification_code:{email.Trim()}:{purpose}";

    public async Task RemoveAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default)
    {
        await cacheService.RemoveAsync(BuildKey(email, purpose), cancellationToken);
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
