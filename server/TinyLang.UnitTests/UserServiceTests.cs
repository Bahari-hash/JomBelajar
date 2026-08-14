using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证用户资料隔离、管理员查询和管理操作的业务规则。
/// </summary>
public sealed class UserServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProfileQueriesShouldKeepPrivateAndPublicFieldsSeparated()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("alice", UserRole.Admin, Now.AddDays(-2));
        user.Nickname = "Alice";
        user.AvatarUrl = "https://oss.example.test/alice.jpg";
        user.Bio = "Learner";
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var current = await service.GetCurrentProfileAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        var publicProfile = await service.GetPublicProfileAsync(
            user.Id,
            TestContext.Current.CancellationToken);

        current.Email.Should().Be(user.Email);
        current.Role.Should().Be(UserRole.Admin);
        current.CreatedAt.Should().Be(user.CreatedAt);
        publicProfile.Should().BeEquivalentTo(new PublicUserProfileResponse(
            user.Id,
            "Alice",
            user.AvatarUrl,
            "Learner"));
        typeof(PublicUserProfileResponse).GetProperties().Select(value => value.Name)
            .Should().BeEquivalentTo(["Id", "Nickname", "AvatarUrl", "Bio"]);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PublicProfileShouldHideBannedOrDeletedUsers(
        bool isBanned,
        bool isDeleted)
    {
        await using var db = CreateDbContext();
        var user = CreateUser("hidden", UserRole.User, Now);
        user.IsBanned = isBanned;
        user.IsDeleted = isDeleted;
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = async () => await service.GetPublicProfileAsync(
            user.Id,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AdminListShouldExcludeDeletedUsersAndCalculateSessionSummaries()
    {
        await using var db = CreateDbContext();
        var older = CreateUser("older", UserRole.User, Now.AddDays(-2));
        var newer = CreateUser("newer", UserRole.Admin, Now.AddDays(-1));
        var deleted = CreateUser("deleted", UserRole.User, Now);
        deleted.IsDeleted = true;
        AddSession(older, Now.AddHours(-4), Now.AddHours(1), false, older.TokenVersion);
        AddSession(older, Now.AddHours(-3), Now.AddHours(-1), false, older.TokenVersion);
        AddSession(older, Now.AddHours(-2), Now.AddHours(1), true, older.TokenVersion);
        AddSession(older, Now.AddHours(-1), Now.AddHours(1), false, older.TokenVersion + 1);
        db.Users.AddRange(older, newer, deleted);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetAdminListAsync(
            new AdminUserListRequest(),
            TestContext.Current.CancellationToken);

        response.TotalCount.Should().Be(2);
        response.Items.Select(value => value.Id).Should().Equal(newer.Id, older.Id);
        var olderResponse = response.Items.Single(value => value.Id == older.Id);
        olderResponse.ActiveSessionCount.Should().Be(1);
        olderResponse.LastLoginAt.Should().Be(Now.AddHours(-1));
        response.Items.Single(value => value.Id == newer.Id).LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task AdminListShouldCombineStatusRoleAndCaseInsensitiveKeywordFilters()
    {
        await using var db = CreateDbContext();
        var matching = CreateUser("matched-admin", UserRole.Admin, Now);
        matching.Email = "Admin@Example.Test";
        matching.IsBanned = true;
        var active = CreateUser("active-admin", UserRole.Admin, Now.AddMinutes(-1));
        var user = CreateUser("matched-user", UserRole.User, Now.AddMinutes(-2));
        user.IsBanned = true;
        db.Users.AddRange(matching, active, user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetAdminListAsync(
            new AdminUserListRequest
            {
                Keyword = "admin@example",
                Role = "Admin",
                Status = AdminUserStatus.Banned
            },
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle().Which.Id.Should().Be(matching.Id);
    }

    [Fact]
    public async Task AdminListShouldSupportDeletedGuidNicknameAndWhitespaceFilters()
    {
        await using var db = CreateDbContext();
        var active = CreateUser("active", UserRole.User, Now.AddMinutes(-1));
        active.Nickname = "Language Learner";
        var deleted = CreateUser("deleted", UserRole.User, Now);
        deleted.IsDeleted = true;
        db.Users.AddRange(active, deleted);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var deletedResult = await service.GetAdminListAsync(
            new AdminUserListRequest { Status = AdminUserStatus.Deleted },
            TestContext.Current.CancellationToken);
        var guidResult = await service.GetAdminListAsync(
            new AdminUserListRequest { Keyword = active.Id.ToString() },
            TestContext.Current.CancellationToken);
        var nicknameResult = await service.GetAdminListAsync(
            new AdminUserListRequest { Keyword = "language learner" },
            TestContext.Current.CancellationToken);
        var whitespaceResult = await service.GetAdminListAsync(
            new AdminUserListRequest { Keyword = "   " },
            TestContext.Current.CancellationToken);

        deletedResult.Items.Should().ContainSingle().Which.Id.Should().Be(deleted.Id);
        guidResult.Items.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        nicknameResult.Items.Should().ContainSingle().Which.Id.Should().Be(active.Id);
        whitespaceResult.Items.Should().ContainSingle().Which.Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task AdminListShouldPageStableOrderingWithoutDuplicates()
    {
        await using var db = CreateDbContext();
        var sameCreatedAt = Enumerable.Range(1, 3)
            .Select(index => CreateUser($"user-{index}", UserRole.User, Now))
            .ToArray();
        db.Users.AddRange(sameCreatedAt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var first = await service.GetAdminListAsync(
            new AdminUserListRequest { Page = 1, PageSize = 2 },
            TestContext.Current.CancellationToken);
        var second = await service.GetAdminListAsync(
            new AdminUserListRequest { Page = 2, PageSize = 2 },
            TestContext.Current.CancellationToken);

        first.TotalCount.Should().Be(3);
        first.TotalPages.Should().Be(2);
        first.Items.Should().HaveCount(2);
        second.Items.Should().ContainSingle();
        first.Items.Concat(second.Items).Select(value => value.Id)
            .Should().OnlyHaveUniqueItems().And.BeEquivalentTo(
                sameCreatedAt.Select(value => value.Id));
    }

    [Fact]
    public async Task AdminDetailShouldIncludeDeletedUserAndReturnZeroActiveSessions()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("deleted", UserRole.Admin, Now);
        user.Nickname = "Former User";
        user.Bio = "Historical bio";
        user.IsDeleted = true;
        user.DeletedAt = Now;
        AddSession(user, Now.AddMinutes(-5), Now.AddHours(1), false, user.TokenVersion);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.GetAdminByIdAsync(
            user.Id,
            TestContext.Current.CancellationToken);

        response.IsDeleted.Should().BeTrue();
        response.Bio.Should().Be("Historical bio");
        response.LastLoginAt.Should().Be(Now.AddMinutes(-5));
        response.ActiveSessionCount.Should().Be(0);
        var forbiddenProperties = new[]
        {
            "PasswordHash",
            "TokenVersion",
            "RefreshTokens",
            "TokenHash",
            "ClientIp",
            "DeviceInfo"
        };
        typeof(AdminUserListItemResponse).GetProperties().Select(value => value.Name)
            .Should().NotContain(forbiddenProperties);
        typeof(AdminUserDetailResponse).GetProperties().Select(value => value.Name)
            .Should().NotContain(forbiddenProperties);
    }

    [Fact]
    public async Task UpdateProfileShouldNormalizeValuesAndRejectMissingUser()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("profile", UserRole.User, Now);
        user.AvatarUrl = "https://media.example.test/old-avatar.png";
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.UpdateProfileAsync(
            user.Id,
            new UpdateProfileRequest
            {
                Nickname = "  Learner  ",
                Bio = "  Studying English  "
            },
            TestContext.Current.CancellationToken);
        var missingAction = async () => await service.UpdateProfileAsync(
            Guid.NewGuid(),
            new UpdateProfileRequest(),
            TestContext.Current.CancellationToken);

        response.Nickname.Should().Be("Learner");
        response.AvatarUrl.Should().Be("https://media.example.test/old-avatar.png");
        response.Bio.Should().Be("Studying English");
        await missingAction.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateProfileShouldAcceptOwnedActiveAvatarResource()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("profile", UserRole.User, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var avatar = CreateAvatarResource(user.Id, ResourceStatus.Active);
        db.MediaResources.Add(avatar);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.UpdateProfileAsync(
            user.Id,
            new UpdateProfileRequest
            {
                AvatarMediaResourceId = avatar.Id
            },
            TestContext.Current.CancellationToken);

        response.AvatarUrl.Should().Be(avatar.Url);
    }

    [Fact]
    public async Task UpdateProfileShouldRejectOtherUsersAvatarResource()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("profile", UserRole.User, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var avatar = CreateAvatarResource(Guid.NewGuid(), ResourceStatus.Active);
        db.MediaResources.Add(avatar);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = async () => await service.UpdateProfileAsync(
            user.Id,
            new UpdateProfileRequest
            {
                AvatarMediaResourceId = avatar.Id
            },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage(ErrorCodes.AvatarResourceOwnershipMismatch.GetMessage());
        user.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task BanShouldRejectSelfAndInvalidateOnlyTheFirstBan()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("target", UserRole.User, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sessions = new Mock<IUserSessionService>();
        var service = CreateService(db, sessions);
        var operatorId = Guid.NewGuid();

        var selfAction = async () => await service.BanAsync(
            user.Id,
            user.Id,
            new BanUserRequest { Reason = "reason" },
            TestContext.Current.CancellationToken);
        await selfAction.Should().ThrowAsync<ForbiddenException>();

        await service.BanAsync(
            operatorId,
            user.Id,
            new BanUserRequest { Reason = "  abuse  " },
            TestContext.Current.CancellationToken);
        await service.BanAsync(
            operatorId,
            user.Id,
            new BanUserRequest { Reason = "abuse" },
            TestContext.Current.CancellationToken);

        user.IsBanned.Should().BeTrue();
        user.BannedAt.Should().Be(Now);
        user.BannedReason.Should().Be("abuse");
        sessions.Verify(value => value.InvalidateAllAsync(
            user,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task RepeatedBanWithNewReasonShouldPreserveBanTimeAndSessions()
    {
        await using var db = CreateDbContext();
        var bannedAt = Now.AddDays(-1);
        var user = CreateUser("target", UserRole.User, Now.AddDays(-2));
        user.IsBanned = true;
        user.BannedAt = bannedAt;
        user.BannedReason = "old";
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sessions = new Mock<IUserSessionService>();
        var service = CreateService(db, sessions);

        await service.BanAsync(
            Guid.NewGuid(),
            user.Id,
            new BanUserRequest { Reason = " new " },
            TestContext.Current.CancellationToken);

        user.BannedAt.Should().Be(bannedAt);
        user.BannedReason.Should().Be("new");
        sessions.Verify(value => value.InvalidateAllAsync(
            It.IsAny<User>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnbanShouldClearBanStateWithoutInvalidatingSessions()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("target", UserRole.User, Now);
        user.IsBanned = true;
        user.BannedAt = Now;
        user.BannedReason = "reason";
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sessions = new Mock<IUserSessionService>();
        var service = CreateService(db, sessions);

        await service.UnbanAsync(
            Guid.NewGuid(),
            user.Id,
            TestContext.Current.CancellationToken);
        await service.UnbanAsync(
            Guid.NewGuid(),
            user.Id,
            TestContext.Current.CancellationToken);

        user.IsBanned.Should().BeFalse();
        user.BannedAt.Should().BeNull();
        user.BannedReason.Should().BeNull();
        sessions.Verify(value => value.InvalidateAllAsync(
            It.IsAny<User>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RoleChangeShouldRejectSelfAndInvalidateOnlyActualChanges()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("target", UserRole.User, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sessions = new Mock<IUserSessionService>();
        var service = CreateService(db, sessions);

        var selfAction = async () => await service.UpdateRoleAsync(
            user.Id,
            user.Id,
            new UpdateRoleRequest { Role = "Admin" },
            TestContext.Current.CancellationToken);
        await selfAction.Should().ThrowAsync<ForbiddenException>();

        await service.UpdateRoleAsync(
            Guid.NewGuid(),
            user.Id,
            new UpdateRoleRequest { Role = "User" },
            TestContext.Current.CancellationToken);
        var response = await service.UpdateRoleAsync(
            Guid.NewGuid(),
            user.Id,
            new UpdateRoleRequest { Role = "Admin" },
            TestContext.Current.CancellationToken);

        response.Role.Should().Be(UserRole.Admin);
        sessions.Verify(value => value.InvalidateAllAsync(
            user,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Editor")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public async Task RoleChangeShouldDefensivelyRejectNonCanonicalRoles(string role)
    {
        await using var db = CreateDbContext();
        var user = CreateUser("target", UserRole.User, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sessions = new Mock<IUserSessionService>();
        var service = CreateService(db, sessions);

        var action = async () => await service.UpdateRoleAsync(
            Guid.NewGuid(),
            user.Id,
            new UpdateRoleRequest { Role = role },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<RequestValidationException>();
        user.Role.Should().Be(UserRole.User);
        sessions.Verify(value => value.InvalidateAllAsync(
            It.IsAny<User>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminListShouldDefensivelyRejectInvalidRoleFilter()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var action = async () => await service.GetAdminListAsync(
            new AdminUserListRequest { Role = "1" },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task AdminMutationsShouldRejectDeletedTargets()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("deleted", UserRole.User, Now);
        user.IsDeleted = true;
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var operatorId = Guid.NewGuid();

        var banAction = async () => await service.BanAsync(
            operatorId,
            user.Id,
            new BanUserRequest { Reason = "reason" },
            TestContext.Current.CancellationToken);
        var unbanAction = async () => await service.UnbanAsync(
            operatorId,
            user.Id,
            TestContext.Current.CancellationToken);
        var roleAction = async () => await service.UpdateRoleAsync(
            operatorId,
            user.Id,
            new UpdateRoleRequest { Role = "Admin" },
            TestContext.Current.CancellationToken);

        await banAction.Should().ThrowAsync<NotFoundException>();
        await unbanAction.Should().ThrowAsync<NotFoundException>();
        await roleAction.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BanShouldDefensivelyRejectMissingReason(string? reason)
    {
        await using var db = CreateDbContext();
        var user = CreateUser("target", UserRole.User, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var action = async () => await service.BanAsync(
            Guid.NewGuid(),
            user.Id,
            new BanUserRequest { Reason = reason! },
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    private static UserService CreateService(
        ApplicationDbContext db,
        Mock<IUserSessionService>? sessionService = null)
        => new(
            db,
            (sessionService ?? new Mock<IUserSessionService>()).Object,
            new TestTimeProvider(Now),
            NullLogger<UserService>.Instance);

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(
        string username,
        UserRole role,
        DateTimeOffset createdAt)
        => new()
        {
            Email = $"{username}@example.test",
            PasswordHash = "not-used",
            Role = role,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

    private static void AddSession(
        User user,
        DateTimeOffset loginAt,
        DateTimeOffset expiresAt,
        bool isRevoked,
        int tokenVersion)
        => user.RefreshTokens.Add(new RefreshToken
        {
            User = user,
            UserId = user.Id,
            TokenHash = Guid.NewGuid().ToString("N"),
            LoginAt = loginAt,
            ExpiresAt = expiresAt,
            IsRevoked = isRevoked,
            TokenVersion = tokenVersion
        });

    private static MediaResource CreateAvatarResource(
        Guid uploaderId,
        ResourceStatus status)
        => new()
        {
            UploaderId = uploaderId,
            ObjectName = $"avatars/{Guid.NewGuid():N}.png",
            OriginalName = "avatar.png",
            Module = ResourceModule.Avatar,
            Status = status,
            Size = 1024,
            Extension = ".png",
            ContentType = "image/png",
            Url = status == ResourceStatus.Active
                ? $"https://oss.example.test/avatars/{Guid.NewGuid():N}.png"
                : null
        };
}
