using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using TinyLang.Enums;
using TinyLang.Infrastructure;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class VerificationCodeStoreTests
{
    [Fact]
    public async Task ShouldSaveVerificationCodeAsRedisStringWithExpiration()
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
        var store = CreateStore(connection.Object, 10);

        await store.SaveAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            "hashed-code",
            TestContext.Current.CancellationToken);

        database.Verify(x => x.StringSetAsync(
            It.Is<RedisKey>(key => key.ToString() == "tiny-langverification_code:v2:user@example.com:Register"),
            "hashed-code",
            It.Is<TimeSpan?>(expiry => expiry == TimeSpan.FromMinutes(10)),
            false,
            When.Always,
            CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task ShouldReadVerificationCodeFromRedisString()
    {
        var database = new Mock<IDatabase>();
        var connection = CreateConnection(database);
        database.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"hashed-code");
        var store = CreateStore(connection.Object, 10);

        var result = await store.GetAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            TestContext.Current.CancellationToken);

        result.Should().Be("hashed-code");
        database.Verify(x => x.StringGetAsync(
            "tiny-langverification_code:v2:user@example.com:Register",
            CommandFlags.None),
            Times.Once);
    }

    [Fact]
    public async Task ShouldAtomicallyConsumeMatchingVerificationCode()
    {
        var database = new Mock<IDatabase>();
        var connection = CreateConnection(database);
        database.Setup(x => x.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)1));
        var store = CreateStore(connection.Object, 10);

        var result = await store.TryConsumeAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            "hashed-code",
            TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        database.Verify(x => x.ScriptEvaluateAsync(
            It.Is<string>(script => script.Contains("GET") && script.Contains("DEL")),
            It.Is<RedisKey[]>(keys => keys.Length == 1 &&
                keys[0].ToString() == "tiny-langverification_code:v2:user@example.com:Register"),
            It.Is<RedisValue[]>(values => values.Length == 1 && values[0] == "hashed-code"),
            CommandFlags.None),
            Times.Once);
    }

    private static VerificationCodeStore CreateStore(
        IConnectionMultiplexer connection,
        int expirationMinutes)
        => new(
            connection,
            Options.Create(new VerificationCodeSettings
            {
                CodeLength = 6,
                ExpMinutes = expirationMinutes
            }));

    private static Mock<IConnectionMultiplexer> CreateConnection(Mock<IDatabase> database)
    {
        var connection = new Mock<IConnectionMultiplexer>();
        connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);
        return connection;
    }
}
