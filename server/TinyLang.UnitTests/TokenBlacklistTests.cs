using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using TinyLang.Infrastructure;

namespace TinyLang.UnitTests;

public sealed class TokenBlacklistTests
{
    [Fact]
    public async Task ShouldBlacklistAccessTokenByTokenId()
    {
        var cache = new TestDistributedCache();
        var blacklist = new TokenBlacklist(cache);
        var tokenId = Guid.NewGuid().ToString("N");

        await blacklist.AddAccessTokenAsync(
            tokenId,
            DateTimeOffset.UtcNow.AddMinutes(5),
            TestContext.Current.CancellationToken);

        (await blacklist.ContainsAccessTokenAsync(
            tokenId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await blacklist.ContainsAccessTokenAsync(
            Guid.NewGuid().ToString("N"),
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldNotBlacklistExpiredAccessToken()
    {
        var cache = new TestDistributedCache();
        var blacklist = new TokenBlacklist(cache);
        var tokenId = Guid.NewGuid().ToString("N");

        await blacklist.AddAccessTokenAsync(
            tokenId,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            TestContext.Current.CancellationToken);

        (await blacklist.ContainsAccessTokenAsync(
            tokenId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    private sealed class TestDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, Entry> entries = new();

        public byte[]? Get(string key)
            => GetEntry(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult(GetEntry(key));

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key) => entries.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            entries.Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => entries[key] = new Entry(value, GetExpiration(options));

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        private byte[]? GetEntry(string key)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (entry.ExpiresAt is not null && entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                entries.Remove(key);
                return null;
            }

            return entry.Value;
        }

        private static DateTimeOffset? GetExpiration(DistributedCacheEntryOptions options)
            => options.AbsoluteExpiration ??
                (options.AbsoluteExpirationRelativeToNow is { } relative
                    ? DateTimeOffset.UtcNow.Add(relative)
                    : null);

        private sealed record Entry(byte[] Value, DateTimeOffset? ExpiresAt);
    }
}
