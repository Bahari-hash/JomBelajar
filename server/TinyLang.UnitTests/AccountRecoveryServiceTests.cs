using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证匿名密码找回不会枚举账户，并在成功时撤销现有会话。
/// </summary>
public sealed class AccountRecoveryServiceTests
{
    [Fact]
    public async Task ForgotPasswordTokenShouldSendOnlyForActiveKnownAccount()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("learner@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sender = new Mock<IVerificationCodeSender>();
        var service = CreateAuthService(db, sender);

        await service.SendForgotPasswordTokenAsync(
            " LEARNER@EXAMPLE.TEST ",
            TestContext.Current.CancellationToken);

        sender.Verify(value => value.SendCodeAsync(
            "learner@example.test",
            VerificationCodePurpose.ResetPassword,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordTokenShouldSilentlyIgnoreUnknownAccount()
    {
        await using var db = CreateDbContext();
        var sender = new Mock<IVerificationCodeSender>();
        var service = CreateAuthService(db, sender);

        await service.SendForgotPasswordTokenAsync(
            "unknown@example.test",
            TestContext.Current.CancellationToken);

        sender.Verify(value => value.SendCodeAsync(
            It.IsAny<string>(),
            It.IsAny<VerificationCodePurpose>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetForgottenPasswordShouldUpdateHashAndInvalidateSessions()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("learner@example.test");
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var authService = new Mock<IAuthService>();
        authService.Setup(value => value.VerifyCodeAsync(
                "learner@example.test",
                VerificationCodePurpose.ResetPassword,
                "123456",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var hasher = new Mock<ISecretHasher>();
        hasher.Setup(value => value.Hash("new-password")).Returns("new-hash");
        var sessions = new Mock<IUserSessionService>();
        var service = CreateSecurityService(db, authService, hasher, sessions);

        await service.ResetForgottenPasswordAsync(
            new ForgotPasswordRequest
            {
                Email = " LEARNER@EXAMPLE.TEST ",
                NewPassword = "new-password",
                VerificationCode = "123456"
            },
            TestContext.Current.CancellationToken);

        user.PasswordHash.Should().Be("new-hash");
        sessions.Verify(value => value.InvalidateAllAsync(
            user,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetForgottenPasswordShouldHideUnknownAccountAsInvalidCode()
    {
        await using var db = CreateDbContext();
        var authService = new Mock<IAuthService>();
        var service = CreateSecurityService(
            db,
            authService,
            new Mock<ISecretHasher>(),
            new Mock<IUserSessionService>());

        var action = async () => await service.ResetForgottenPasswordAsync(
            new ForgotPasswordRequest
            {
                Email = "unknown@example.test",
                NewPassword = "new-password",
                VerificationCode = "123456"
            },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<UnauthorizedException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.VerificationCodeInvalid);
        authService.Verify(value => value.VerifyCodeAsync(
            It.IsAny<string>(),
            It.IsAny<VerificationCodePurpose>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AuthService CreateAuthService(
        ApplicationDbContext db,
        Mock<IVerificationCodeSender> sender)
        => new(
            db,
            Mock.Of<ISecretHasher>(),
            sender.Object,
            Mock.Of<IJwtTokenService>(),
            Mock.Of<ITokenBlacklist>(),
            Mock.Of<IDatabaseExceptionClassifier>(),
            Mock.Of<IUserSessionService>(),
            Options.Create(new JwtSettings
            {
                JwtSecret = new string('s', 32),
                Issuer = "tests",
                Audience = "tests",
                AccessTokenExpMinutes = 15,
                RefreshTokenExpMinutes = 1440
            }));

    private static AccountSecurityService CreateSecurityService(
        ApplicationDbContext db,
        Mock<IAuthService> authService,
        Mock<ISecretHasher> hasher,
        Mock<IUserSessionService> sessions)
        => new(
            db,
            hasher.Object,
            authService.Object,
            sessions.Object,
            Mock.Of<IDatabaseExceptionClassifier>());

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(string email)
        => new()
        {
            Username = email,
            Email = email,
            PasswordHash = "old-hash"
        };
}
