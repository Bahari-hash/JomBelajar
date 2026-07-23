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

    public async Task SendRegisterTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        if (await db.Users.AnyAsync(x => x.Email == email && !x.IsDeleted, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }

        await verificationCodeSender.SendCodeAsync(email, VerificationCodePurpose.Register, cancellationToken);
    }

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

        await tokenBlacklist.AddAsync(request.RefreshToken, stored.ExpiresAt, cancellationToken);
        return await IssueTokensAsync(stored.User, clientIp, deviceInfo, cancellationToken);
    }

    public async Task LogoutAsync(Guid userId, string? accessToken, string? refreshToken, CancellationToken cancellationToken = default)
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
        if (accessToken is not null)
        {
            await tokenBlacklist.AddAsync(accessToken, now.AddMinutes(_jwtSettings.AccessTokenExpMinutes), cancellationToken);
        }
        await tokenBlacklist.AddAsync(refreshToken, stored.ExpiresAt, cancellationToken);
    }

    public async Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
        await userSessionService.InvalidateAllAsync(user, cancellationToken);
    }

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

    public async Task SendResetPasswordTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        await verificationCodeSender.SendCodeAsync(user.Email, VerificationCodePurpose.ResetPassword, cancellationToken);
    }

    public async Task SendDeleteAccountTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        await verificationCodeSender.SendCodeAsync(user.Email, VerificationCodePurpose.DeleteAccount, cancellationToken);
    }

    public Task<bool> VerifyCodeAsync(
        string email,
        VerificationCodePurpose purpose,
        string code,
        CancellationToken cancellationToken = default)
        => verificationCodeSender.VerifyCodeAsync(NormalizeEmail(email), purpose, code, cancellationToken);

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

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task<User> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    private static UserResponse ToResponse(User user) => new(user.Id, user.Email, user.Role);
}
