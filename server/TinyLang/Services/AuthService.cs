using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 实现注册、凭据验证、令牌签发轮换和认证验证码流程。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="secretHasher">密码散列和验证服务。</param>
/// <param name="verificationCodeSender">验证码发送和消费服务。</param>
/// <param name="jwtTokenService">JWT 和 refresh token 服务。</param>
/// <param name="tokenBlacklist">已撤销令牌缓存。</param>
/// <param name="databaseExceptionClassifier">数据库约束异常分类器。</param>
/// <param name="userSessionService">用户会话失效服务。</param>
/// <param name="jwtOptions">JWT 有效期配置。</param>
public sealed class AuthService(
    IApplicationDbContext db,
    ISecretHasher secretHasher,
    IVerificationCodeSender verificationCodeSender,
    IJwtTokenService jwtTokenService,
    ITokenBlacklist tokenBlacklist,
    IDatabaseExceptionClassifier databaseExceptionClassifier,
    IUserSessionService userSessionService,
    IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    /// <inheritdoc />
    public async Task SendRegisterTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        if (await db.Users.AnyAsync(x => x.Email == email && !x.IsDeleted, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }

        await verificationCodeSender.SendCodeAsync(email, VerificationCodePurpose.Register, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (!await verificationCodeSender.VerifyCodeAsync(email, VerificationCodePurpose.Register, request.VerificationCode, cancellationToken))
        {
            throw UnauthorizedException.Create(ErrorCodes.VerificationCodeInvalid);
        }
        if (await db.Users.AnyAsync(x => x.Email == email && !x.IsDeleted, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }

        var user = new User
        {
            Username = email,
            Email = email,
            PasswordHash = secretHasher.Hash(request.Password),
            Role = UserRole.User
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            "IX_users_Email",
            "IX_users_Username"))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }
        return ToResponse(user);
    }

    /// <inheritdoc />
    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, string? clientIp, string? deviceInfo, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && !x.IsDeleted, cancellationToken);
        if (user is null || user.IsBanned || !secretHasher.Verify(request.Password, user.PasswordHash))
        {
            throw UnauthorizedException.Create(ErrorCodes.InvalidCredentials);
        }

        return await IssueTokensAsync(user, clientIp, deviceInfo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthTokenResponse> RefreshAsync(RefreshTokenRequest request, string? clientIp, string? deviceInfo, CancellationToken cancellationToken = default)
    {
        var hash = jwtTokenService.HashRefreshToken(request.RefreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= now ||
            stored.User.IsDeleted || stored.User.IsBanned ||
            stored.TokenVersion != stored.User.TokenVersion)
        {
            throw UnauthorizedException.Create(ErrorCodes.RefreshTokenInvalid);
        }

        var affectedRows = await db.RefreshTokens
            .Where(x => x.Id == stored.Id &&
                !x.IsRevoked &&
                x.ExpiresAt > now &&
                x.TokenVersion == x.User.TokenVersion &&
                !x.User.IsDeleted &&
                !x.User.IsBanned)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRevoked, true)
                .SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.LastUsedAt, now)
                .SetProperty(x => x.UsageCount, x => x.UsageCount + 1), cancellationToken);
        if (affectedRows != 1)
        {
            throw UnauthorizedException.Create(ErrorCodes.RefreshTokenInvalid);
        }

        await tokenBlacklist.AddRefreshTokenAsync(request.RefreshToken, stored.ExpiresAt, cancellationToken);
        return await IssueTokensAsync(stored.User, clientIp, deviceInfo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(
        Guid userId,
        string accessTokenId,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (refreshToken is null)
        {
            throw UnauthorizedException.Create(ErrorCodes.RefreshTokenInvalid);
        }

        var hash = jwtTokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(
            x => x.TokenHash == hash && x.UserId == userId && !x.IsRevoked,
            cancellationToken) ?? throw UnauthorizedException.Create(ErrorCodes.RefreshTokenInvalid);
        stored.IsRevoked = true;
        stored.RevokedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await tokenBlacklist.AddAccessTokenAsync(accessTokenId, accessTokenExpiresAt, cancellationToken);
        await tokenBlacklist.AddRefreshTokenAsync(refreshToken, stored.ExpiresAt, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
        await userSessionService.InvalidateAllAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendChangeEmailTokenAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        newEmail = NormalizeEmail(newEmail);
        if (newEmail == user.Email)
        {
            throw new RequestValidationException(ErrorCodes.EmailUnchanged);
        }
        if (await db.Users.AnyAsync(x => x.Email == newEmail && !x.IsDeleted, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }

        await verificationCodeSender.SendCodeAsync(newEmail, VerificationCodePurpose.ChangeEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendResetPasswordTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        await verificationCodeSender.SendCodeAsync(user.Email, VerificationCodePurpose.ResetPassword, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendForgotPasswordTokenAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        var exists = await db.Users.AnyAsync(
            x => x.Email == email && !x.IsDeleted && !x.IsBanned,
            cancellationToken);
        if (!exists)
        {
            return;
        }

        await verificationCodeSender.SendCodeAsync(
            email,
            VerificationCodePurpose.ResetPassword,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendDeleteAccountTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        await verificationCodeSender.SendCodeAsync(user.Email, VerificationCodePurpose.DeleteAccount, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> VerifyCodeAsync(
        string email,
        VerificationCodePurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
        => verificationCodeSender.VerifyCodeAsync(NormalizeEmail(email), purpose, code, cancellationToken);

    /// <summary>
    /// 签发 access token 和 refresh token，并持久化 refresh token 会话。
    /// </summary>
    /// <param name="user">令牌所属用户。</param>
    /// <param name="clientIp">客户端 IP 地址。</param>
    /// <param name="deviceInfo">客户端设备描述。</param>
    /// <param name="cancellationToken">用于取消持久化操作的令牌。</param>
    /// <returns>新签发的令牌及用户信息。</returns>
    private async Task<AuthTokenResponse> IssueTokensAsync(User user, string? clientIp, string? deviceInfo, CancellationToken cancellationToken)
    {
        var (token, expiresAt) = jwtTokenService.CreateAccessToken(user);
        var refresh = jwtTokenService.CreateRefreshToken();
        var refreshExp = DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.RefreshTokenExpMinutes);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenVersion = user.TokenVersion,
            TokenHash = jwtTokenService.HashRefreshToken(refresh),
            ClientIp = clientIp,
            DeviceInfo = deviceInfo,
            ExpiresAt = refreshExp,
            LoginAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new AuthTokenResponse(token, refresh, (long)(expiresAt - DateTimeOffset.UtcNow).TotalSeconds, ToResponse(user));
    }

    /// <summary>
    /// 去除邮箱两端空白并转换为小写形式。
    /// </summary>
    /// <param name="email">待规范化邮箱。</param>
    /// <returns>规范化后的邮箱。</returns>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// 查找尚未软删除的用户。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>已跟踪的有效用户。</returns>
    /// <exception cref="NotFoundException">用户不存在或已软删除。</exception>
    private async Task<User> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    /// <summary>
    /// 将用户实体投影为认证响应摘要。
    /// </summary>
    /// <param name="user">用户实体。</param>
    /// <returns>用户认证摘要。</returns>
    private static UserResponse ToResponse(User user) => new(user.Id, user.Email, user.Role);
}
