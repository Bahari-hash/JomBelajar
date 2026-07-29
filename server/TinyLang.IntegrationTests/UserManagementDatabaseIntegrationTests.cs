using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Services;

namespace TinyLang.IntegrationTests;

/// <summary>
/// 使用真实 PostgreSQL 验证用户管理查询、索引和会话失效事务。
/// </summary>
public sealed class UserManagementDatabaseIntegrationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 16, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// 在隔离 schema 中验证管理查询和会话撤销的完整数据库语义。
    /// </summary>
    [Fact]
    public async Task UserManagementQueriesAndSessionInvalidationShouldWorkAtomically()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL user management tests.");
        }

        var schema = $"tiny_lang_users_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteSchemaCommandAsync(
            adminConnection,
            $"CREATE SCHEMA \"{schema}\"",
            TestContext.Current.CancellationToken);

        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(schemaConnection)
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;
            await using (var migrationDb = new ApplicationDbContext(options))
            {
                await migrationDb.Database.MigrateAsync(
                    TestContext.Current.CancellationToken);
            }

            await VerifyIndexesAsync(schemaConnection);
            await VerifyQueryTranslationAndAtomicBanAsync(options);
            await VerifyFailedUserSaveRollsBackTokenRevocationAsync(options);
        }
        finally
        {
            await ExecuteSchemaCommandAsync(
                adminConnection,
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 验证 migration 已创建三项用户管理复合索引。
    /// </summary>
    private static async Task VerifyIndexesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT indexname FROM pg_indexes WHERE schemaname = current_schema() AND tablename = 'users'",
            connection);
        await using var reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        names.Should().Contain([
            "IX_users_IsDeleted_CreatedAt_Id",
            "IX_users_IsDeleted_IsBanned_CreatedAt_Id",
            "IX_users_IsDeleted_Role_CreatedAt_Id"
        ]);
    }

    /// <summary>
    /// 验证 PostgreSQL 能翻译筛选和会话聚合，并原子完成首次封禁。
    /// </summary>
    private static async Task VerifyQueryTranslationAndAtomicBanAsync(
        DbContextOptions<ApplicationDbContext> options)
    {
        Guid userId;
        await using (var setupDb = new ApplicationDbContext(options))
        {
            var user = CreateUser("query-editor", UserRole.Editor);
            user.RefreshTokens.Add(CreateRefreshToken(user, "query-1", Now.AddHours(-2)));
            user.RefreshTokens.Add(CreateRefreshToken(user, "query-2", Now.AddHours(-1)));
            setupDb.Users.Add(user);
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            userId = user.Id;
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var sessionService = new UserSessionService(db, new FixedTimeProvider(Now));
            var userService = new UserService(
                db,
                sessionService,
                new FixedTimeProvider(Now),
                NullLogger<UserService>.Instance);
            var list = await userService.GetAdminListAsync(
                new AdminUserListRequest
                {
                    Keyword = "QUERY-EDITOR@EXAMPLE.TEST",
                    Role = UserRole.Editor,
                    Status = AdminUserStatus.Active
                },
                TestContext.Current.CancellationToken);

            list.Items.Should().ContainSingle();
            list.Items[0].ActiveSessionCount.Should().Be(2);
            list.Items[0].LastLoginAt.Should().Be(Now.AddHours(-1));

            await userService.BanAsync(
                Guid.NewGuid(),
                userId,
                new BanUserRequest { Reason = "policy violation" },
                TestContext.Current.CancellationToken);
        }

        await using var verificationDb = new ApplicationDbContext(options);
        var persistedUser = await verificationDb.Users.AsNoTracking().SingleAsync(
            value => value.Id == userId,
            TestContext.Current.CancellationToken);
        var tokens = await verificationDb.RefreshTokens.AsNoTracking()
            .Where(value => value.UserId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
        persistedUser.IsBanned.Should().BeTrue();
        persistedUser.BannedReason.Should().Be("policy violation");
        persistedUser.TokenVersion.Should().Be(1);
        tokens.Should().OnlyContain(value => value.IsRevoked && value.RevokedAt == Now);
    }

    /// <summary>
    /// 验证 tracked 用户保存失败时批量 refresh token 更新被事务回滚。
    /// </summary>
    private static async Task VerifyFailedUserSaveRollsBackTokenRevocationAsync(
        DbContextOptions<ApplicationDbContext> options)
    {
        Guid targetId;
        var duplicateEmail = "duplicate@example.test";
        await using (var setupDb = new ApplicationDbContext(options))
        {
            var existing = CreateUser("existing", UserRole.User);
            existing.Email = duplicateEmail;
            var target = CreateUser("rollback-target", UserRole.User);
            target.RefreshTokens.Add(CreateRefreshToken(
                target,
                "rollback-token",
                Now.AddMinutes(-5)));
            setupDb.Users.AddRange(existing, target);
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            targetId = target.Id;
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var user = await db.Users.SingleAsync(
                value => value.Id == targetId,
                TestContext.Current.CancellationToken);
            user.Email = duplicateEmail;
            var sessionService = new UserSessionService(db, new FixedTimeProvider(Now));

            var action = async () => await sessionService.InvalidateAllAsync(
                user,
                TestContext.Current.CancellationToken);

            await action.Should().ThrowAsync<DbUpdateException>();
        }

        await using var verificationDb = new ApplicationDbContext(options);
        var persistedUser = await verificationDb.Users.AsNoTracking().SingleAsync(
            value => value.Id == targetId,
            TestContext.Current.CancellationToken);
        var token = await verificationDb.RefreshTokens.AsNoTracking().SingleAsync(
            value => value.UserId == targetId,
            TestContext.Current.CancellationToken);
        persistedUser.Email.Should().Be("rollback-target@example.test");
        persistedUser.TokenVersion.Should().Be(0);
        token.IsRevoked.Should().BeFalse();
        token.RevokedAt.Should().BeNull();
    }

    /// <summary>
    /// 创建集成测试用户。
    /// </summary>
    private static User CreateUser(string username, UserRole role)
        => new()
        {
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = "not-used",
            Role = role
        };

    /// <summary>
    /// 创建尚未撤销且仍有效的 refresh token 会话。
    /// </summary>
    private static RefreshToken CreateRefreshToken(
        User user,
        string tokenHash,
        DateTimeOffset loginAt)
        => new()
        {
            User = user,
            UserId = user.Id,
            TokenHash = tokenHash,
            TokenVersion = user.TokenVersion,
            LoginAt = loginAt,
            ExpiresAt = Now.AddHours(1)
        };

    /// <summary>
    /// 在测试 schema 中执行固定结构的 DDL。
    /// </summary>
    private static async Task ExecuteSchemaCommandAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 为集成测试提供固定 UTC 时间。
    /// </summary>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
