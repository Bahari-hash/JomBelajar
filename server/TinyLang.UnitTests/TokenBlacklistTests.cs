using FluentAssertions;
using Moq;
using StackExchange.Redis;
using TinyLang.Infrastructure;

namespace TinyLang.UnitTests;

public sealed class TokenBlacklistTests
{
    [Fact]
    public async Task ShouldStoreAccessTokenBlacklistAsRedisString()
    {
        var database = new Mock<IDatabase>();
        var connection = CreateConnection(database);
        database.Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var blacklist = new TokenBlacklist(connection.Object);

        await blacklist.AddAccessTokenAsync(
            "access-token-id",
            DateTimeOffset.UtcNow.AddMinutes(5),
            TestContext.Current.CancellationToken);

        database.Verify(x => x.StringSetAsync(
            It.Is<RedisKey>(key => key.ToString().StartsWith("tiny-langauth:blacklist:access:")),
            "1",
            It.Is<TimeSpan?>(expiry => expiry > TimeSpan.Zero && expiry <= TimeSpan.FromMinutes(5)),
            false,
            When.Always,
            CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task ShouldUseKeyExistsWhenCheckingAccessTokenBlacklist()
    {
        var database = new Mock<IDatabase>();
        var connection = CreateConnection(database);
        database.Setup(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        var blacklist = new TokenBlacklist(connection.Object);

        var result = await blacklist.ContainsAccessTokenAsync(
            "access-token-id",
            TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        database.Verify(x => x.KeyExistsAsync(
            It.Is<RedisKey>(key => key.ToString().StartsWith("tiny-langauth:blacklist:access:")),
            CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task ShouldNotWriteExpiredAccessTokenBlacklist()
    {
        var database = new Mock<IDatabase>();
        var connection = CreateConnection(database);
        var blacklist = new TokenBlacklist(connection.Object);

        await blacklist.AddAccessTokenAsync(
            "access-token-id",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            TestContext.Current.CancellationToken);

        database.Verify(x => x.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()),
            Times.Never);
    }

    private static Mock<IConnectionMultiplexer> CreateConnection(Mock<IDatabase> database)
    {
        var connection = new Mock<IConnectionMultiplexer>();
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        return connection;
    }
}
